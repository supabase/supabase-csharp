#region

using System;
using System.Threading.Tasks;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateOAuthClient;

#endregion

namespace Gotrue.Tests.CreateOAuthClient;

/// <summary>
///     Pins the exact bytes creating an OAuth client puts on the wire: the required fields, the optional settings when set,
///     and their absence when left to the server's defaults.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateOAuthClientApprovalTests : RequestApprovalFixture
{
    [TestInitialize]
    public void AnswerWithClient() => this.RespondTo("/admin/oauth/clients", OAuthClientResponses.Confidential);

    [TestMethod]
    public async Task CreateOAuthClientRequest_ShouldSendOnlyRequiredFields_GivenConfidentialClientWithoutOptions()
    {
        await this.Api.CreateOAuthClientAsync("service-jwt", NewOAuthClient.Confidential("My App", new[] { new Uri("https://app.example.com/callback") }));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task CreateOAuthClientRequest_ShouldSendEverySetting_GivenPublicClientWithOptions()
    {
        await this.Api.CreateOAuthClientAsync("service-jwt",
            NewOAuthClient.Public("My SPA", new[] { new Uri("http://localhost:3000/callback"), new Uri("myapp://callback") }),
            new OAuthClientOptions
            {
                GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode, OAuthClientGrantType.RefreshToken },
                ClientUri = new Uri("https://app.example.com"),
                LogoUri = new Uri("https://app.example.com/logo.png"),
            });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
