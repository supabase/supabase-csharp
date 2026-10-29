#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateCustomProvider;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.CreateCustomProvider;

/// <summary>
///     End-to-end creation of a custom OAuth/OIDC provider against the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class CreateCustomProviderTests : CustomProviderAdminFixture
{
    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldRegisterProvider()
    {
        var created = await this.Admin.CreateCustomProviderAsync(this.NewOAuth2Provider(), new CustomProviderOptions
        {
            Scopes = new[] { "email" },
            PkceEnabled = false,
            AttributeMapping = new Dictionary<string, object> { ["name"] = "full_name" },
            CustomClaimsAllowlist = new[] { "groups" },
            AuthorizationParameters = new Dictionary<string, string> { ["prompt"] = "consent" },
            EmailOptional = true,
        });
        created.Should().BeEquivalentTo(new
        {
            Type = CustomProviderType.OAuth2,
            Identifier = this.Identifier,
            Name = "E2E provider",
            ClientId = "client-id",
            Scopes = new[] { "email" },
            PkceEnabled = false,
            AttributeMapping = new Dictionary<string, object> { ["name"] = "full_name" },
            CustomClaimsAllowlist = new[] { "groups" },
            AuthorizationParameters = new Dictionary<string, string> { ["prompt"] = "consent" },
            Enabled = true,
            EmailOptional = true,
            AuthorizationUrl = new Uri("https://example.com/authorize"),
            TokenUrl = new Uri("https://example.com/token"),
            UserInfoUrl = new Uri("https://example.com/userinfo"),
        }, "the auth server stores the settings sent and applies its default for those left out, such as enabled");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldThrowConflict_GivenIdentifierAlreadyRegistered()
    {
        await this.SeedProvider();
        var create = () => this.Admin.CreateCustomProviderAsync(this.NewOAuth2Provider());
        await create.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "conflict",
                "the auth server answers 400 conflict when a provider already has the identifier");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var create = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).CreateCustomProviderAsync(this.NewOAuth2Provider());
        await create.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private NewCustomProvider NewOAuth2Provider() =>
        NewCustomProvider.OAuth2(this.Identifier, "E2E provider", "client-id", "client-secret",
            new Uri("https://example.com/authorize"), new Uri("https://example.com/token"), new Uri("https://example.com/userinfo"));
}
