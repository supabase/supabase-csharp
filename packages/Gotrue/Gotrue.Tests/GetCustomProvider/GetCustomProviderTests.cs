#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.GetCustomProvider;

/// <summary>
///     End-to-end lookup of a custom OAuth/OIDC provider by its identifier against the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class GetCustomProviderTests : CustomProviderAdminFixture
{
    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldReturnProvider()
    {
        await this.SeedProvider();
        (await this.Admin.GetCustomProviderAsync(this.Identifier)).Should().BeEquivalentTo(new
        {
            Type = CustomProviderType.OAuth2,
            Identifier = this.Identifier,
            Name = "E2E provider",
            ClientId = "client-id",
            AuthorizationUrl = new Uri("https://example.com/authorize"),
            TokenUrl = new Uri("https://example.com/token"),
            UserInfoUrl = new Uri("https://example.com/userinfo"),
        }, "the provider is returned as it was registered");
    }

    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldThrowCustomProviderNotFound_GivenUnknownIdentifier()
    {
        var get = () => this.Admin.GetCustomProviderAsync(this.Identifier);
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.CustomProviderNotFound,
                "the auth server answers 404 custom_provider_not_found when no provider has the identifier");
    }

    [TestMethod]
    public async Task GetCustomProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        await this.SeedProvider();
        var get = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).GetCustomProviderAsync(this.Identifier);
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }
}
