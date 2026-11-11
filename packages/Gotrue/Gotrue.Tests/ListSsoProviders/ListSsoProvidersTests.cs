#region

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.ListSsoProviders;

#endregion

namespace Gotrue.Tests.ListSsoProviders;

/// <summary>
///     End-to-end listing of single sign-on identity providers against the live stack: unfiltered, by exact resource
///     ID and by resource ID prefix. Every resource ID starts with a fresh GUID, so the filtered lists only ever hold
///     the providers the test registered.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ListSsoProvidersTests : SsoProviderAdminFixture
{
    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldListProviderWithoutMetadataXml_GivenNoFilter()
    {
        var id = await this.SeedProvider(UniqueResourceId());
        (await this.Admin.ListSsoProvidersAsync()).Should().ContainSingle(provider => provider.Id == id).Which.Should().BeEquivalentTo(new
        {
            Domains = new[] { this.Domain },
            Saml = new { EntityId = this.EntityId, MetadataXml = (string?) null },
        }, "the auth server lists every provider with its domains and SAML configuration, leaving out the metadata XML");
    }

    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldListOnlyExactMatch_GivenResourceId()
    {
        var resourceId = UniqueResourceId();
        var id = await this.SeedProvider(resourceId);
        await this.SeedAnotherProvider($"{resourceId}-eu");
        (await this.Admin.ListSsoProvidersAsync(SsoProviderFilter.ByResourceId(resourceId))).Select(provider => provider.Id).Should().Equal(new[] { id },
            "the auth server lists only the provider whose resource_id equals the filter");
    }

    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldListProvidersStartingWithPrefix_GivenResourceIdPrefix()
    {
        var prefix = $"{UniqueResourceId()}-";
        var europe = await this.SeedProvider($"{prefix}eu");
        var america = await this.SeedAnotherProvider($"{prefix}us");
        await this.SeedAnotherProvider($"x{prefix}");
        (await this.Admin.ListSsoProvidersAsync(SsoProviderFilter.ByResourceIdPrefix(prefix))).Select(provider => provider.Id).Should().BeEquivalentTo(new[] { europe, america },
            "the auth server lists the providers whose resource_id starts with resource_id_prefix");
    }

    [TestMethod]
    [DataRow("_")]
    [DataRow("%")]
    [DataRow("\\")]
    public async Task ListSsoProvidersAsync_ShouldMatchPrefixLiterally_GivenPatternCharacter(string patternCharacter)
    {
        var stem = UniqueResourceId();
        var literal = await this.SeedProvider($"{stem}{patternCharacter}1");
        await this.SeedAnotherProvider($"{stem}x1");
        (await this.Admin.ListSsoProvidersAsync(SsoProviderFilter.ByResourceIdPrefix($"{stem}{patternCharacter}"))).Select(provider => provider.Id).Should().Equal(new[] { literal },
            "the auth server matches the prefix with an unescaped SQL LIKE, so the SDK escapes the pattern characters for the prefix to mean what it says");
    }

    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldReturnEmptyList_GivenPrefixNoProviderStartsWith()
    {
        (await this.Admin.ListSsoProvidersAsync(SsoProviderFilter.ByResourceIdPrefix(UniqueResourceId()))).Should().BeEmpty(
            "the auth server answers an empty items array when no provider matches");
    }

    [TestMethod]
    public async Task ListSsoProvidersAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var list = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).ListSsoProvidersAsync();
        await list.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private static string UniqueResourceId() => Guid.NewGuid().ToString("N");
}
