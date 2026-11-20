#region

using System;
using System.Threading.Tasks;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.UpdateOAuthClient;

#endregion

namespace Gotrue.Tests.UpdateOAuthClient;

/// <summary>
///     Pins the exact bytes updating an OAuth client puts on the wire: only the members that are set, and every
///     member when all are.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateOAuthClientApprovalTests : RequestApprovalFixture
{
    private static readonly Guid ClientId = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b");

    [TestInitialize]
    public void AnswerWithClient() => this.RespondTo($"/admin/oauth/clients/{ClientId}", OAuthClientResponses.Confidential);

    [TestMethod]
    public async Task UpdateOAuthClientRequest_ShouldSendOnlyChangedMembers_GivenName()
    {
        await this.Api.UpdateOAuthClientAsync("service-jwt", ClientId, new OAuthClientChanges { Name = "My App (production)" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateOAuthClientRequest_ShouldSendEveryMember_GivenAllChanged()
    {
        await this.Api.UpdateOAuthClientAsync("service-jwt", ClientId, new OAuthClientChanges
        {
            Name = "My App",
            RedirectUris = new[] { new Uri("https://app.example.com/callback"), new Uri("myapp://callback") },
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretPost,
            Options = new OAuthClientOptions
            {
                GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode },
                ClientUri = new Uri("https://app.example.com"),
                LogoUri = new Uri("https://app.example.com/logo.png"),
            },
        });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
