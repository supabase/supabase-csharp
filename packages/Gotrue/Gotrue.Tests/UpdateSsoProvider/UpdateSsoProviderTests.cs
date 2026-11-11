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
using Supabase.Gotrue.UpdateSsoProvider;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.UpdateSsoProvider;

/// <summary>
///     End-to-end changes to a single sign-on identity provider against the live stack: what each change replaces,
///     keeps and clears, and the rejections the auth server answers with.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class UpdateSsoProviderTests : SsoProviderAdminFixture
{
    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldReplaceDomainsAndKeepEverythingElse_GivenCurrentProvider()
    {
        var resourceId = RandomString(12);
        await this.SeedProvider(resourceId);
        var anotherDomain = $"{RandomString(12)}.example.com";
        var current = await this.Admin.GetSsoProviderAsync(SsoProviderReference.ByResourceId(resourceId));
        var updated = await this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(current.Id),
            SsoProviderChanges.KeepingNameIdFormatOf(current) with { Domains = new[] { this.Domain, anotherDomain } });
        updated.Should().BeEquivalentTo(current with { Domains = new[] { this.Domain, anotherDomain }, UpdatedAt = updated.UpdatedAt },
            "a partial update replaces the domains sent and keeps the settings left out, the NameID format sent back included");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldStoreEverySetting_GivenNameIdFormatChangedWithAttributeMapping()
    {
        var id = await this.SeedProvider(RandomString(12));
        var resourceId = RandomString(12);
        await this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(id), SsoProviderChanges.WithNameIdFormat(SamlNameIdFormat.Persistent) with
        {
            Metadata = SamlMetadata.FromXml(this.MetadataXml),
            ResourceId = resourceId,
            Enabled = false,
            AttributeMapping = new Dictionary<string, SamlAttribute>
            {
                ["groups"] = new() { Names = new[] { "memberOf" }, KeepsAllValues = true },
                ["role"] = new() { Default = "member" },
            },
        });
        (await this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(id))).Should().BeEquivalentTo(new
        {
            ResourceId = resourceId,
            Enabled = false,
            Domains = new[] { this.Domain },
            Saml = new
            {
                EntityId = this.EntityId,
                NameIdFormat = SamlNameIdFormat.Persistent,
                AttributeMapping = new Dictionary<string, SamlAttribute>
                {
                    ["groups"] = new() { Names = new[] { "memberOf" }, KeepsAllValues = true },
                    ["role"] = new() { Default = "member" },
                },
            },
        }, "the auth server stores a new NameID format when it writes the SAML configuration for a new attribute mapping");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldClearEverySetting_GivenEmptyValuesWithoutNameIdFormat()
    {
        var id = await this.SeedProvider(RandomString(12));
        await this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(id), SsoProviderChanges.WithoutNameIdFormat() with
        {
            Domains = Array.Empty<string>(),
            ResourceId = string.Empty,
            AttributeMapping = new Dictionary<string, SamlAttribute>(),
        });
        (await this.Admin.GetSsoProviderAsync(SsoProviderReference.ById(id))).Should().BeEquivalentTo(new
        {
            ResourceId = (string?) null,
            Domains = Array.Empty<string>(),
            Saml = new
            {
                NameIdFormat = (SamlNameIdFormat?) null,
                AttributeMapping = new Dictionary<string, SamlAttribute>(),
            },
        }, "an empty domain list, resource ID or attribute mapping clears it, and a NameID format left out is cleared with the mapping");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowSamlEntityIdMismatch_GivenMetadataOfAnotherEntity()
    {
        var id = await this.SeedProvider(RandomString(12));
        var otherEntityId = $"https://idp.{RandomString(12)}.example.com/saml";
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(id),
            SsoProviderChanges.WithoutNameIdFormat() with { Metadata = SamlMetadata.FromXml(MetadataXmlFor(otherEntityId)) });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "saml_entity_id_mismatch",
                "the auth server answers 400 saml_entity_id_mismatch when new metadata carries another entity ID than the provider's");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowSsoDomainAlreadyExists_GivenDomainOfAnotherProvider()
    {
        await this.SeedProvider(RandomString(12));
        var anotherId = await this.SeedAnotherProvider(RandomString(12));
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ById(anotherId),
            SsoProviderChanges.WithoutNameIdFormat() with { Domains = new[] { this.Domain } });
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "sso_domain_already_exists",
                "the auth server answers 400 sso_domain_already_exists when another provider already has the domain");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowSsoProviderNotFound_GivenUnknownResourceId()
    {
        var update = () => this.Admin.UpdateSsoProviderAsync(SsoProviderReference.ByResourceId(RandomString(12)), SsoProviderChanges.WithoutNameIdFormat());
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.SsoProviderNotFound,
                "the auth server answers 404 sso_provider_not_found when no provider has the resource ID");
    }

    [TestMethod]
    public async Task UpdateSsoProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var id = await this.SeedProvider(RandomString(12));
        var update = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey)
            .UpdateSsoProviderAsync(SsoProviderReference.ById(id), SsoProviderChanges.WithoutNameIdFormat());
        await update.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }
}
