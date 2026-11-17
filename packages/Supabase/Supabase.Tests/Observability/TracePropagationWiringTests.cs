using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;

namespace Supabase.Tests.Observability;

/// <summary>
/// <see cref="SupabaseOptions.TracePropagation"/> reaches every sub-client the umbrella and the stateless facade build, and a
/// <see cref="Supabase.Storage.ClientOptions"/> that disables it is honored too (client.observability.trace_propagation).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class TracePropagationWiringTests
{
    private static Supabase.Client ClientWith(SupabaseOptions options) => new("http://localhost", "test-key", options);

    [TestMethod]
    public void SupabaseOptions_ShouldEnableTracePropagation_GivenNoChoice() =>
        new SupabaseOptions().TracePropagation.Should().Be(TracePropagation.Enabled, "propagation is on unless the caller opts out");

    [TestMethod]
    public void Client_ShouldPassDisabledToEverySubClient_GivenPropagationDisabled()
    {
        var client = ClientWith(new SupabaseOptions { TracePropagation = TracePropagation.Disabled });
        using (new AssertionScope())
        {
            client.Auth.Options.TracePropagation.Should().Be(TracePropagation.Disabled);
            client.Postgrest.Options.TracePropagation.Should().Be(TracePropagation.Disabled);
            ((Supabase.Functions.Client) client.Functions).Options.TracePropagation.Should().Be(TracePropagation.Disabled);
            client.Storage.Options.TracePropagation.Should().Be(TracePropagation.Disabled);
        }
    }

    [TestMethod]
    public void Client_ShouldPassEnabledToEverySubClient_GivenTheDefault()
    {
        var client = ClientWith(new SupabaseOptions());
        using (new AssertionScope())
        {
            client.Auth.Options.TracePropagation.Should().Be(TracePropagation.Enabled);
            client.Postgrest.Options.TracePropagation.Should().Be(TracePropagation.Enabled);
            ((Supabase.Functions.Client) client.Functions).Options.TracePropagation.Should().Be(TracePropagation.Enabled);
            client.Storage.Options.TracePropagation.Should().Be(TracePropagation.Enabled);
        }
    }

    [TestMethod]
    public void Client_ShouldKeepStoragePropagationDisabled_GivenOnlyTheStorageOptionsDisableIt() =>
        ClientWith(new SupabaseOptions { StorageClientOptions = new Storage.ClientOptions { TracePropagation = TracePropagation.Disabled } })
            .Storage.Options.TracePropagation.Should().Be(TracePropagation.Disabled, "a Storage-level opt-out is not overridden by the umbrella default");

    [TestMethod]
    public void StatelessClient_ShouldPassDisabledToEverySubClient_GivenPropagationDisabled()
    {
        var options = new SupabaseOptions { TracePropagation = TracePropagation.Disabled };
        using (new AssertionScope())
        {
            StatelessClient.GetAuthOptions("http://localhost", "test-key", options).TracePropagation.Should().Be(TracePropagation.Disabled);
            StatelessClient.GetRestOptions("test-key", options).TracePropagation.Should().Be(TracePropagation.Disabled);
            ((Supabase.Functions.Client) StatelessClient.Functions("http://localhost", "test-key", options)).Options.TracePropagation
                .Should().Be(TracePropagation.Disabled);
            StatelessClient.Storage("http://localhost", "test-key", options).Options.TracePropagation.Should().Be(TracePropagation.Disabled);
        }
    }
}
