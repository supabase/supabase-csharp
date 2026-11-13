#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateSsoProvider;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.CreateSsoProvider;

/// <summary>
///     End-to-end registration of a single sign-on identity provider from its SAML metadata XML against the live
///     stack, and the rejections the auth server answers with.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class CreateSsoProviderTests : SsoProviderAdminFixture
{
    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldRegisterProvider_GivenMetadataXmlWithOptions()
    {
        var resourceId = $"{RandomString(8)}/eu";
        var created = await this.Admin.CreateSsoProviderAsync(SamlMetadata.FromXml(this.MetadataXml), new SsoProviderOptions
        {
            Domains = new[] { this.Domain },
            ResourceId = resourceId,
            NameIdFormat = SamlNameIdFormat.EmailAddress,
            Enabled = false,
            AttributeMapping = new Dictionary<string, SamlAttribute>
            {
                ["email"] = new() { Names = new[] { "mail", "email" } },
                ["groups"] = new() { Names = new[] { "memberOf" }, KeepsAllValues = true },
                ["role"] = new() { Default = "member" },
            },
        });
        this.RemoveAfterTest(created.Id);
        created.Should().BeEquivalentTo(new
        {
            ResourceId = resourceId,
            Enabled = false,
            Domains = new[] { this.Domain },
            Saml = new
            {
                EntityId = this.EntityId,
                MetadataXml = this.MetadataXml,
                MetadataUrl = (Uri?) null,
                NameIdFormat = SamlNameIdFormat.EmailAddress,
                AttributeMapping = new Dictionary<string, SamlAttribute>
                {
                    ["email"] = new() { Names = new[] { "mail", "email" } },
                    ["groups"] = new() { Names = new[] { "memberOf" }, KeepsAllValues = true },
                    ["role"] = new() { Default = "member" },
                },
            },
        }, "the auth server stores every setting sent, including a resource ID with '/', and reads the entity ID from the metadata");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldApplyServerDefaults_GivenNoOptions()
    {
        var created = await this.Admin.CreateSsoProviderAsync(SamlMetadata.FromXml(this.MetadataXml));
        this.RemoveAfterTest(created.Id);
        created.Should().BeEquivalentTo(new
        {
            ResourceId = (string?) null,
            Enabled = true,
            Domains = Array.Empty<string>(),
            Saml = new
            {
                EntityId = this.EntityId,
                NameIdFormat = (SamlNameIdFormat?) null,
                AttributeMapping = new Dictionary<string, SamlAttribute>(),
            },
        }, "a provider registered with nothing optional is enabled, with no resource ID, domains, NameID format or attribute mapping");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowValidationFailed_GivenHttpMetadataUrl()
    {
        var create = () => this.Admin.CreateSsoProviderAsync(SamlMetadata.FromUrl(new Uri($"http://{this.Domain}/saml/metadata")));
        await create.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "validation_failed",
                "the auth server only fetches metadata over HTTPS and answers 400 validation_failed to any other scheme");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowUnexpectedFailure_GivenUnparseableMetadataXml()
    {
        var create = () => this.Admin.CreateSsoProviderAsync(SamlMetadata.FromXml("<md:EntityDescriptor"));
        await create.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "unexpected_failure",
                "the auth server does not classify a metadata parse error and answers 500 unexpected_failure");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowSamlIdpAlreadyExists_GivenEntityIdAlreadyRegistered()
    {
        await this.SeedProvider(RandomString(12));
        var create = () => this.Admin.CreateSsoProviderAsync(SamlMetadata.FromXml(this.MetadataXml));
        await create.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "saml_idp_already_exists",
                "the auth server answers 422 saml_idp_already_exists when a provider already has the metadata's entity ID");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowSsoDomainAlreadyExists_GivenDomainAlreadyAssigned()
    {
        await this.SeedProvider(RandomString(12));
        var otherEntityId = $"https://idp.{RandomString(12)}.example.com/saml";
        var create = () => this.Admin.CreateSsoProviderAsync(SamlMetadata.FromXml(MetadataXmlFor(otherEntityId)),
            new SsoProviderOptions { Domains = new[] { this.Domain } });
        await create.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "sso_domain_already_exists",
                "the auth server answers 400 sso_domain_already_exists when another provider already has the domain");
    }

    [TestMethod]
    public async Task CreateSsoProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var create = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).CreateSsoProviderAsync(SamlMetadata.FromXml(this.MetadataXml));
        await create.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }
}
