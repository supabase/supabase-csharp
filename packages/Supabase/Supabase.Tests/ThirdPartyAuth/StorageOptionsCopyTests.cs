using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;

namespace Supabase.Tests.ThirdPartyAuth;

/// <summary>
/// With <see cref="SupabaseOptions.ThirdPartyAccessToken"/> set, Storage receives a copy of the caller's
/// <see cref="Storage.ClientOptions"/> carrying the callback, so the caller's object is never modified. Every public
/// setting of <see cref="Storage.ClientOptions"/> is discovered by reflection and given a non-default value: a setting
/// added to Storage later and not carried by the copy fails here, whatever value the caller would give it
/// (client.authentication_integration.third_party_auth).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class StorageOptionsCopyTests
{
    private static readonly Func<CancellationToken, Task<string?>> SignedIn = _ => Task.FromResult<string?>("a-third-party-jwt");

    private static readonly IReadOnlyDictionary<Type, Func<int, object>> NonDefaultValues = new Dictionary<Type, Func<int, object>>
    {
        [typeof(TimeSpan)] = position => TimeSpan.FromSeconds(1000 + position),
        [typeof(HttpClient)] = _ => new HttpClient(),
        [typeof(IWebProxy)] = _ => new WebProxy("http://proxy.example.com"),
        [typeof(RetryOptions)] = position => new RetryOptions { MaxRetries = position + 1 },
    };

    public static IEnumerable<object[]> CarriedSettings => CarriedSettingsOf().Select(setting => new object[] { setting.Name });

    [TestMethod]
    [DynamicData(nameof(CarriedSettings))]
    public void Client_ShouldCarryTheStorageSetting_GivenAThirdPartyAccessToken(string settingName)
    {
        var callerOptions = OptionsWithEveryCarriedSettingChanged();
        var client = new Supabase.Client("http://localhost", "test-key",
            new SupabaseOptions { ThirdPartyAccessToken = SignedIn, StorageClientOptions = callerOptions });
        var setting = CarriedSettingsOf().Single(candidate => candidate.Name == settingName);
        setting.ValueIn(client.Storage.Options).Should().Be(setting.ValueIn(callerOptions),
            $"the copy Storage receives must keep the caller's {settingName}; add it to the umbrella's copy of the Storage options");
    }

    [TestMethod]
    public void CarriedSettings_ShouldCoverEveryPublicStorageSetting() =>
        CarriedSettingsOf().Should().HaveCountGreaterThan(1, "reflection must discover Storage's settings for the copy test to guard anything");

    private static Storage.ClientOptions OptionsWithEveryCarriedSettingChanged()
    {
        var options = new Storage.ClientOptions();
        foreach (var (setting, position) in CarriedSettingsOf().Select((setting, position) => (setting, position)))
            setting.Assign(options, NonDefaultValueFor(setting, position));
        return options;
    }

    private static object NonDefaultValueFor(Setting setting, int position) =>
        NonDefaultValues.TryGetValue(setting.Type, out var create)
            ? create(position)
            : throw new AssertFailedException(
                $"Storage.ClientOptions.{setting.Name} is a {setting.Type.Name}, which this test cannot yet give a non-default value: add one to {nameof(NonDefaultValues)}.");

    private static IEnumerable<Setting> CarriedSettingsOf() =>
        typeof(Storage.ClientOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .Select(Setting.Of)
            .Concat(typeof(Storage.ClientOptions).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(Setting.Of))
            .Where(setting => setting.Name != nameof(Storage.ClientOptions.RetrieveAccessToken))
            .OrderBy(setting => setting.Name, StringComparer.Ordinal);

    private sealed record Setting(string Name, Type Type, Func<object, object?> ValueIn, Action<object, object?> Assign)
    {
        internal static Setting Of(PropertyInfo property) => new(property.Name, property.PropertyType, property.GetValue, property.SetValue);

        internal static Setting Of(FieldInfo field) => new(field.Name, field.FieldType, field.GetValue, field.SetValue);
    }
}
