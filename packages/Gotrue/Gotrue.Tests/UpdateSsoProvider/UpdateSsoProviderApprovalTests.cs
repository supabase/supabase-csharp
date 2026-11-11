#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.UpdateSsoProvider;

#endregion

namespace Gotrue.Tests.UpdateSsoProvider;

/// <summary>
///     Pins the exact bytes changing a single sign-on identity provider puts on the wire: only the settings set, empty
///     values for those cleared, and the <c>NameID</c> format every change states.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateSsoProviderApprovalTests : RequestApprovalFixture
{
    private const string ProviderPath = "/admin/sso/providers/0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76";

    private static readonly SsoProviderReference Provider = SsoProviderReference.ById(Guid.Parse("0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76"));

    [TestInitialize]
    public void AnswerWithProvider() => this.RespondTo(ProviderPath, SsoProviderResponses.Complete);

    [TestMethod]
    public async Task UpdateSsoProviderRequest_ShouldSendNothing_GivenNoChangeWithoutNameIdFormat()
    {
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider, SsoProviderChanges.WithoutNameIdFormat());
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateSsoProviderRequest_ShouldSendCurrentNameIdFormatWithDomains_GivenChangesKeepingNameIdFormat()
    {
        var current = new SsoProvider { Saml = new SamlConfiguration { NameIdFormat = SamlNameIdFormat.EmailAddress } };
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider,
            SsoProviderChanges.KeepingNameIdFormatOf(current) with { Domains = new[] { "acme.com", "acme.org" } });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateSsoProviderRequest_ShouldSendNoNameIdFormat_GivenChangesKeepingAbsentNameIdFormat()
    {
        var current = new SsoProvider { Saml = new SamlConfiguration() };
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider, SsoProviderChanges.KeepingNameIdFormatOf(current));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateSsoProviderRequest_ShouldSendEverySetting_GivenMetadataUrlWithEveryChange()
    {
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider, SsoProviderChanges.WithNameIdFormat(SamlNameIdFormat.Persistent) with
        {
            Metadata = SamlMetadata.FromUrl(new Uri("https://idp.acme.com/saml/metadata")),
            Domains = new[] { "acme.com" },
            ResourceId = "acme",
            Enabled = false,
            AttributeMapping = new Dictionary<string, SamlAttribute>
            {
                ["email"] = new() { Names = new[] { "mail", "email" } },
                ["groups"] = new() { Names = new[] { "memberOf" }, KeepsAllValues = true },
                ["role"] = new() { Default = "member" },
            },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateSsoProviderRequest_ShouldSendMetadataXml_GivenMetadataXml()
    {
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider, SsoProviderChanges.WithoutNameIdFormat() with
        {
            Metadata = SamlMetadata.FromXml("""<md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.acme.com/saml"/>"""),
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateSsoProviderRequest_ShouldSendEmptyValues_GivenClearedSettings()
    {
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider, SsoProviderChanges.WithoutNameIdFormat() with
        {
            Domains = Array.Empty<string>(),
            ResourceId = string.Empty,
            AttributeMapping = new Dictionary<string, SamlAttribute>(),
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task UpdateSsoProviderRequest_ShouldSendDisabledAsOppositeOfEnabled(bool enabled)
    {
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider, SsoProviderChanges.WithoutNameIdFormat() with { Enabled = enabled });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data").UseParameters(enabled);
    }

    [TestMethod]
    [DataRow(SamlNameIdFormat.Persistent)]
    [DataRow(SamlNameIdFormat.EmailAddress)]
    [DataRow(SamlNameIdFormat.Transient)]
    [DataRow(SamlNameIdFormat.Unspecified)]
    public async Task UpdateSsoProviderRequest_ShouldSendNameIdFormatAsItsUrn(SamlNameIdFormat format)
    {
        await this.Api.UpdateSsoProviderAsync("service-jwt", Provider, SsoProviderChanges.WithNameIdFormat(format));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data").UseParameters(format);
    }
}
