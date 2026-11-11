#region

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Pins the wire contract for linking a native OIDC identity to the signed-in user: the SDK posts the
///     id_token grant with link_identity set, authenticated as the current user, and the client adopts the
///     session GoTrue returns. Parity with gotrue-js signInWithIdToken({ ..., link_identity: true }).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class LinkIdentityWithIdTokenContractTests
{
    private const string LinkedSession =
        """
        {
          "access_token": "linked-access-token",
          "refresh_token": "linked-refresh-token",
          "token_type": "bearer",
          "expires_in": 3600,
          "user": { "id": "linked-user", "aud": "authenticated", "email": "linked@example.com", "email_confirmed_at": "2026-06-26T06:08:11Z" }
        }
        """;

    private IGotrueApi<User, Session> api = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.api = new Api(this.server.Url);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task LinkIdentityWithIdToken_ShouldPostAuthenticatedIdTokenLinkGrant_GivenSupportedProvider()
    {
        this.StubIdTokenGrant();
        await this.api.LinkIdentityWithIdToken("user-access-token", new LinkIdentityWithIdTokenOptions(Constants.Provider.Google, "google-id-token"));
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/token")
            .WithQueryParam("grant_type", "id_token")
            .WithHeader("Authorization", "Bearer user-access-token")
            .WithExactJsonBody("provider", "google")
            .WithExactJsonBody("id_token", "google-id-token")
            .WithBooleanJsonBody("link_identity", true)
            .WithoutJsonBodyField("access_token");
    }

    [TestMethod]
    public async Task LinkIdentityWithIdToken_ShouldIncludeCredentialProofs_GivenAccessTokenNonceAndCaptcha()
    {
        this.StubIdTokenGrant();
        var options = new LinkIdentityWithIdTokenOptions(Constants.Provider.Apple, "apple-id-token", "apple-access-token", "the-nonce", "the-captcha");
        await this.api.LinkIdentityWithIdToken("user-access-token", options);
        this.server.VerifySingleReceivedRequest()
            .WithExactJsonBody("access_token", "apple-access-token")
            .WithExactJsonBody("nonce", "the-nonce")
            .WithNestedJsonBody("gotrue_meta_security", "captcha_token", "the-captcha");
    }

    [TestMethod]
    public async Task LinkIdentityWithIdToken_ShouldAdoptTheReturnedSession_GivenSignedInUser()
    {
        var client = await this.SignedInClient();
        var stateChanges = new List<Constants.AuthState>();
        client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        this.server.Given(Request.Create().WithPath("/token").WithParam("grant_type", "id_token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(LinkedSession));
        await client.LinkIdentityWithIdToken(new LinkIdentityWithIdTokenOptions(Constants.Provider.Google, "google-id-token"));
        using (new AssertionScope())
        {
            client.CurrentSession!.AccessToken.Should().Be("linked-access-token", "a successful link returns a session the client must adopt");
            stateChanges.Should().Contain(SignedIn);
        }
    }

    private void StubIdTokenGrant() =>
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(LinkedSession));

    private async Task<IGotrueClient<User, Session>> SignedInClient()
    {
        this.server.Given(Request.Create().WithPath("/signup").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(LinkedSession));
        var client = TestClients.Against(this.server);
        await client.SignUpAsync(Constants.SignUpType.Email, RandomEmail(), Password);
        this.server.Reset();
        return client;
    }
}
