#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.UpdateCustomProvider;

#endregion

namespace Gotrue.Tests.UpdateCustomProvider;

/// <summary>
///     End-to-end update of a custom OAuth/OIDC provider against the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class UpdateCustomProviderTests : CustomProviderAdminFixture
{
    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldApplyChangesAndKeepTheRest()
    {
        await this.SeedProvider();
        await this.Admin.UpdateCustomProviderAsync(this.Identifier, new CustomProviderChanges
        {
            Name = "Renamed provider",
            TokenUrl = new Uri("https://example.com/v2/token"),
            Options = new CustomProviderOptions { Scopes = new[] { "email" }, Enabled = false },
        });
        (await this.Admin.GetCustomProviderAsync(this.Identifier)).Should().BeEquivalentTo(new
        {
            Name = "Renamed provider",
            ClientId = "client-id",
            Scopes = new[] { "email" },
            Enabled = false,
            AuthorizationUrl = new Uri("https://example.com/authorize"),
            TokenUrl = new Uri("https://example.com/v2/token"),
        }, "the auth server applies only the fields sent and keeps every other setting");
    }

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldThrowCustomProviderNotFound_GivenUnknownIdentifier()
    {
        var update = () => this.Admin.UpdateCustomProviderAsync(this.Identifier, new CustomProviderChanges { Name = "Renamed provider" });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.CustomProviderNotFound,
                "the auth server answers 404 custom_provider_not_found when no provider has the identifier");
    }

    [TestMethod]
    public async Task UpdateCustomProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        await this.SeedProvider();
        var update = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).UpdateCustomProviderAsync(this.Identifier, new CustomProviderChanges { Name = "Renamed provider" });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }
}
