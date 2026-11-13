#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.GetSsoProvider;

/// <summary>
///     End-to-end lookup of a single sign-on identity provider, by id and by resource ID, against the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class GetSsoProviderTests : SsoProviderAdminFixture
{
    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldReturnProviderWithMetadata_GivenId()
    {
        var id = await this.SeedProvider(RandomString(12));
        (await this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(id))).Should().BeEquivalentTo(new
        {
            Id = id,
            Enabled = true,
            Domains = new[] { this.Domain },
            Saml = new
            {
                EntityId = this.EntityId,
                MetadataXml = this.MetadataXml,
                NameIdFormat = SamlNameIdFormat.EmailAddress,
                AttributeMapping = new Dictionary<string, object> { ["email"] = new { Names = new[] { "mail", "email" } } },
            },
        }, "a single provider is returned with its metadata XML, domains, attribute mapping and NameID format");
    }

    [TestMethod]
    [DataRow("plain")]
    [DataRow("eu+$&,:;=@1")]
    [DataRow("a b%?#!*'()é")]
    public async Task GetSsoProviderAsync_ShouldReturnProvider_GivenResourceId(string resourceIdSuffix)
    {
        var resourceId = $"{RandomString(8)}-{resourceIdSuffix}";
        var id = await this.SeedProvider(resourceId);
        (await this.Admin.GetSsoProviderAsync(SsoProviderReference.ByResourceId(resourceId))).Id.Should().Be(id,
            "the auth server addresses a provider by resource_<resource_id> in place of its id, whatever characters the resource ID holds besides '/'");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownId()
    {
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(Guid.NewGuid()));
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider has the id");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownResourceId()
    {
        var get = () => this.Admin.GetSsoProviderAsync(SsoProviderReference.ByResourceId(RandomString(12)));
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider has the resource ID");
    }

    [TestMethod]
    public async Task GetSsoProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var id = await this.SeedProvider(RandomString(12));
        var get = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).GetSsoProviderAsync(SsoProviderReference.ById(id));
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }
}
