#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateCustomProvider;

#endregion

namespace Gotrue.Tests.CreateCustomProvider;

/// <summary>
///     Pins the exact bytes creating a custom provider puts on the wire: the per-protocol required fields, the optional settings
///     when set, and their absence when left to the server's defaults.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateCustomProviderApprovalTests : RequestApprovalFixture
{
    [TestInitialize]
    public void AnswerWithProvider() => this.RespondTo("/admin/custom-providers", CustomProviderResponses.OAuth2);

    [TestMethod]
    public async Task CreateCustomProviderRequest_ShouldSendOnlyRequiredFields_GivenOAuth2ProviderWithoutOptions()
    {
        await this.Api.CreateCustomProviderAsync("service-jwt", NewCustomProvider.OAuth2("custom:customauth", "Custom Auth Provider",
            "client-id", "client-secret", new Uri("https://provider.example.com/oauth/authorize"),
            new Uri("https://provider.example.com/oauth/token"), new Uri("https://provider.example.com/oauth/userinfo")));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateCustomProviderRequest_ShouldSendEverySetting_GivenOidcProviderWithOptions()
    {
        await this.Api.CreateCustomProviderAsync("service-jwt",
            NewCustomProvider.Oidc("custom:mycompany", "My Company SSO", "client-id", "client-secret", new Uri("https://accounts.mycompany.com")),
            new CustomProviderOptions
            {
                AcceptableClientIds = new[] { "ios-client-id" },
                Scopes = new[] { "email", "profile" },
                PkceEnabled = false,
                AttributeMapping = new Dictionary<string, object> { ["email"] = "user_email" },
                CustomClaimsAllowlist = new[] { "groups" },
                AuthorizationParameters = new Dictionary<string, string> { ["prompt"] = "consent" },
                Enabled = false,
                EmailOptional = true,
                DiscoveryUrl = new Uri("https://accounts.mycompany.com/.well-known/openid-configuration"),
                SkipNonceCheck = true,
                JwksUri = new Uri("https://accounts.mycompany.com/jwks"),
            });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
