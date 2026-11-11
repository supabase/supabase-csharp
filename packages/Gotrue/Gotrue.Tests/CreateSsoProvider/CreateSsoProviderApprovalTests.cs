#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateSsoProvider;

#endregion

namespace Gotrue.Tests.CreateSsoProvider;

/// <summary>
///     Pins the exact bytes registering a single sign-on identity provider puts on the wire: the SAML type, the one
///     metadata source given, the optional settings when set, and their absence when left to the server's defaults.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateSsoProviderApprovalTests : RequestApprovalFixture
{
    [TestInitialize]
    public void AnswerWithProvider() => this.RespondTo("/admin/sso/providers", SsoProviderResponses.Complete);

    [TestMethod]
    public async Task CreateSsoProviderRequest_ShouldSendOnlyTypeAndMetadataUrl_GivenUrlWithoutOptions()
    {
        await this.Api.CreateSsoProviderAsync("service-jwt", SamlMetadata.FromUrl(new Uri("https://idp.acme.com/saml/metadata")));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateSsoProviderRequest_ShouldSendOnlyTypeAndMetadataXml_GivenXmlWithEmptyOptions()
    {
        await this.Api.CreateSsoProviderAsync("service-jwt",
            SamlMetadata.FromXml("""<md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.acme.com/saml"/>"""),
            new SsoProviderOptions());
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateSsoProviderRequest_ShouldSendEverySetting_GivenUrlWithOptions()
    {
        await this.Api.CreateSsoProviderAsync("service-jwt", SamlMetadata.FromUrl(new Uri("https://idp.acme.com/saml/metadata")),
            new SsoProviderOptions
            {
                Domains = new[] { "acme.com", "acme.org" },
                ResourceId = "acme",
                NameIdFormat = SamlNameIdFormat.EmailAddress,
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
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateSsoProviderRequest_ShouldSendDisabledAsOppositeOfEnabled(bool enabled)
    {
        await this.Api.CreateSsoProviderAsync("service-jwt", SamlMetadata.FromUrl(new Uri("https://idp.acme.com/saml/metadata")),
            new SsoProviderOptions { Enabled = enabled });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data").UseParameters(enabled);
    }

    [TestMethod]
    [DataRow(SamlNameIdFormat.Persistent)]
    [DataRow(SamlNameIdFormat.EmailAddress)]
    [DataRow(SamlNameIdFormat.Transient)]
    [DataRow(SamlNameIdFormat.Unspecified)]
    public async Task CreateSsoProviderRequest_ShouldSendNameIdFormatAsItsUrn(SamlNameIdFormat format)
    {
        await this.Api.CreateSsoProviderAsync("service-jwt", SamlMetadata.FromUrl(new Uri("https://idp.acme.com/saml/metadata")),
            new SsoProviderOptions { NameIdFormat = format });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data").UseParameters(format);
    }
}
