#region

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.CreateCustomProvider;

#endregion

namespace Gotrue.Tests.CreateCustomProvider;

/// <summary>
///     Building a custom provider to register: the settings each protocol cannot do without, the identifier checks
///     shared with the rest of custom provider administration, and keeping the client secret out of logs.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class NewCustomProviderTests
{
    private const string Identifier = "custom:mycompany";
    private const string Secret = "client-secret";

    private static readonly Uri Endpoint = new("https://provider.example.com/oauth");

    public static IEnumerable<object[]> ProvidersMissingASetting =>
        new[]
        {
            new object[] { "identifier", (Func<NewCustomProvider>) (() => NewCustomProvider.Oidc(null!, "name", "id", Secret, Endpoint)) },
            new object[] { "name", (Func<NewCustomProvider>) (() => NewCustomProvider.Oidc(Identifier, null!, "id", Secret, Endpoint)) },
            new object[] { "clientId", (Func<NewCustomProvider>) (() => NewCustomProvider.Oidc(Identifier, "name", null!, Secret, Endpoint)) },
            new object[] { "clientSecret", (Func<NewCustomProvider>) (() => NewCustomProvider.Oidc(Identifier, "name", "id", null!, Endpoint)) },
            new object[] { "issuer", (Func<NewCustomProvider>) (() => NewCustomProvider.Oidc(Identifier, "name", "id", Secret, null!)) },
            new object[] { "authorizationUrl", (Func<NewCustomProvider>) (() => NewCustomProvider.OAuth2(Identifier, "name", "id", Secret, null!, Endpoint, Endpoint)) },
            new object[] { "tokenUrl", (Func<NewCustomProvider>) (() => NewCustomProvider.OAuth2(Identifier, "name", "id", Secret, Endpoint, null!, Endpoint)) },
            new object[] { "userInfoUrl", (Func<NewCustomProvider>) (() => NewCustomProvider.OAuth2(Identifier, "name", "id", Secret, Endpoint, Endpoint, null!)) },
        };

    [TestMethod]
    [DynamicData(nameof(ProvidersMissingASetting))]
    public void Factory_ShouldThrowArgumentNull_GivenMissingSetting(string parameterName, Func<NewCustomProvider> build) =>
        build.Should().ThrowExactly<ArgumentNullException>().WithParameterName(parameterName);

    [TestMethod]
    [DataRow("mycompany")]
    [DataRow("custom:my/company")]
    public void Factory_ShouldThrowArgument_GivenIdentifierCustomProviderAdministrationCannotAddress(string identifier)
    {
        var build = () => NewCustomProvider.Oidc(identifier, "name", "id", Secret, Endpoint);
        build.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("identifier", "the auth server requires the 'custom:' prefix, and a provider whose identifier changes the route could never be fetched, updated or deleted");
    }

    [TestMethod]
    public void ToString_ShouldLeaveOutClientSecret() =>
        NewCustomProvider.Oidc(Identifier, "My Company", "client-id", Secret, Endpoint).ToString().Should().Be(
            "NewCustomProvider { Type = Oidc, Identifier = custom:mycompany, Name = My Company, ClientId = client-id }",
            "a record prints every property by default, and the secret must never reach a log");
}
