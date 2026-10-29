#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.UpdateCustomProvider;

#endregion

namespace Gotrue.Tests.UpdateCustomProvider;

/// <summary>
///     Pins the exact bytes an update puts on the wire: only the members set, and every member when all are set.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateCustomProviderApprovalTests : RequestApprovalFixture
{
    [TestInitialize]
    public void AnswerWithProvider() => this.RespondTo("/admin/custom-providers/custom:customauth", CustomProviderResponses.OAuth2);

    [TestMethod]
    public async Task UpdateCustomProviderRequest_ShouldSendOnlySetChanges_GivenNameOnly()
    {
        await this.Api.UpdateCustomProviderAsync("service-jwt", "custom:customauth", new CustomProviderChanges { Name = "Renamed" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateCustomProviderRequest_ShouldSendEveryChange_GivenEveryMember()
    {
        await this.Api.UpdateCustomProviderAsync("service-jwt", "custom:customauth", new CustomProviderChanges
        {
            Name = "Renamed",
            ClientId = "new-client-id",
            ClientSecret = "new-client-secret",
            Issuer = new Uri("https://accounts.mycompany.com"),
            AuthorizationUrl = new Uri("https://provider.example.com/v2/authorize"),
            TokenUrl = new Uri("https://provider.example.com/v2/token"),
            UserInfoUrl = new Uri("https://provider.example.com/v2/userinfo"),
            Options = new CustomProviderOptions
            {
                AcceptableClientIds = new[] { "ios-client-id" },
                Scopes = new[] { "email" },
                PkceEnabled = true,
                AttributeMapping = new Dictionary<string, object> { ["name"] = "full_name" },
                CustomClaimsAllowlist = new[] { "org_id" },
                AuthorizationParameters = new Dictionary<string, string> { ["prompt"] = "login" },
                Enabled = false,
                EmailOptional = false,
                DiscoveryUrl = new Uri("https://accounts.mycompany.com/.well-known/openid-configuration"),
                SkipNonceCheck = false,
                JwksUri = new Uri("https://provider.example.com/v2/jwks"),
            },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
