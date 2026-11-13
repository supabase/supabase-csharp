#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.DeleteSsoProvider;

/// <summary>
///     End-to-end removal of a single sign-on identity provider, by id and by resource ID, against the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class DeleteSsoProviderTests : SsoProviderAdminFixture
{
    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldRemoveProvider_GivenId()
    {
        var id = await this.SeedProvider(RandomString(12));
        await this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(id));
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(id));
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "a deleted provider is no longer retrievable by its id");
    }

    [TestMethod]
    [DataRow("plain")]
    [DataRow("eu+$&,:;=@1")]
    [DataRow("a b%?#!*'()é")]
    public async Task DeleteSsoProviderAsync_ShouldRemoveProvider_GivenResourceId(string resourceIdSuffix)
    {
        var resourceId = $"{RandomString(8)}-{resourceIdSuffix}";
        var id = await this.SeedProvider(resourceId);
        await this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ByResourceId(resourceId));
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(id));
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server addresses a provider by resource_<resource_id> in place of its id, whatever characters the resource ID holds besides '/'");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldReleaseDomainAndEntityId()
    {
        var resourceId = RandomString(12);
        await this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(await this.SeedProvider(resourceId)));
        var reregister = () => this.SeedProvider(resourceId);
        await reregister.Should().NotThrowAsync(
            "the auth server deletes a provider's domains with it, so its domain, entity ID and resource ID can be registered again");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenProviderAlreadyDeleted()
    {
        var provider = SsoProviderReference.ById(await this.SeedProvider(RandomString(12)));
        await this.Admin.DeleteSsoProviderAsync(provider);
        var deleteAgain = () => this.Admin.DeleteSsoProviderAsync(provider);
        await deleteAgain.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found once the provider is gone");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownId()
    {
        var delete = () => this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ById(Guid.NewGuid()));
        await delete.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider has the id");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownResourceId()
    {
        var delete = () => this.Admin.DeleteSsoProviderAsync(SsoProviderReference.ByResourceId(RandomString(12)));
        await delete.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider has the resource ID");
    }

    [TestMethod]
    public async Task DeleteSsoProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var id = await this.SeedProvider(RandomString(12));
        var delete = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).DeleteSsoProviderAsync(SsoProviderReference.ById(id));
        await delete.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }
}
