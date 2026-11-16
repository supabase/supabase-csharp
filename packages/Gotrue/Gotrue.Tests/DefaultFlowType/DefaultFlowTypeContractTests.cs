#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.DefaultFlowType;

/// <summary>
///     Pins the client's default OAuth flow type against a stubbed server: a client whose options choose PKCE starts
///     every PKCE-capable flow with a code challenge when the call leaves its own flow type unset, and a flow type set
///     on the call wins over the client's (client.authentication_integration.oauth_flow_type).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DefaultFlowTypeContractTests
{
    private const string Email = "user@example.com";
    private const string SsoDomain = "example.com";
    private static readonly Guid SsoProviderId = Guid.Parse("4c9f2a1e-7b3d-4e6a-9f8c-1d2e3b4a5c6d");
    private IGotrueClient<User, Session> client = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.AgainstWithDefaultFlowType(this.server, OAuthFlowType.PKCE);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignIn_ShouldStartAPkceFlow_GivenNoOptions()
    {
        var state = await this.client.SignIn(Provider.Github);
        state.Uri.Query.Should().Contain("code_challenge=", "a PKCE client starts an OAuth sign-in with a code challenge when the call does not choose a flow (RFC 7636 §4.3)");
    }

    [TestMethod]
    public async Task SignIn_ShouldStartAPkceFlow_GivenOptionsThatLeaveTheFlowTypeUnset()
    {
        var state = await this.client.SignIn(Provider.Github, new SignInOptions { Scopes = "repo" });
        state.PKCEVerifier.Should().NotBeNullOrEmpty("options that do not set a flow type fall back to the client's default");
    }

    [TestMethod]
    public async Task SignIn_ShouldStartAnImplicitFlow_GivenTheCallChoosesImplicit()
    {
        var state = await this.client.SignIn(Provider.Github, new SignInOptions { FlowType = OAuthFlowType.Implicit });
        state.PKCEVerifier.Should().BeNull("a flow type set on the call wins over the client's default");
    }

    [TestMethod]
    public async Task SignIn_ShouldLeaveTheCallerOptionsUnchanged_GivenTheyAreReusedOnAnImplicitClient()
    {
        var options = new SignInOptions();
        await this.client.SignIn(Provider.Github, options);
        var state = await TestClients.Against(this.server).SignIn(Provider.Github, options);
        state.PKCEVerifier.Should().BeNull("the PKCE client resolves its default on a copy, so the caller's options still defer to each client's own default");
    }

    [TestMethod]
    public async Task SignInWithOtp_ShouldSendACodeChallenge_GivenOptionsThatLeaveTheFlowTypeUnset()
    {
        this.StubJsonOk("/otp");
        await this.client.SignInWithOtp(new SignInWithPasswordlessEmailOptions(Email));
        this.server.VerifySingleReceivedRequestTo("/otp").ReadJsonBodyField("code_challenge").Should().NotBeNullOrEmpty(
            "a PKCE client starts a magic link with a code challenge when the call does not choose a flow (RFC 7636 §4.3)");
    }

    [TestMethod]
    public async Task SignInWithOtp_ShouldSendNoCodeChallenge_GivenTheCallChoosesImplicit()
    {
        this.StubJsonOk("/otp");
        await this.client.SignInWithOtp(new SignInWithPasswordlessEmailOptions(Email) { FlowType = OAuthFlowType.Implicit });
        this.server.VerifySingleReceivedRequestTo("/otp").WithoutJsonBodyField("code_challenge");
    }

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldReturnAVerifier_GivenOptionsThatLeaveTheFlowTypeUnset()
    {
        this.StubJsonOk("/recover");
        var state = await this.client.ResetPasswordForEmail(new ResetPasswordForEmailOptions(Email));
        state.PKCEVerifier.Should().NotBeNullOrEmpty("a PKCE client starts a password reset with a code challenge when the call does not choose a flow");
    }

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldReturnNoVerifier_GivenTheCallChoosesImplicit()
    {
        this.StubJsonOk("/recover");
        var state = await this.client.ResetPasswordForEmail(new ResetPasswordForEmailOptions(Email) { FlowType = OAuthFlowType.Implicit });
        state.PKCEVerifier.Should().BeNull("a flow type set on the call wins over the client's default");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldSendACodeChallenge_GivenNoOptions()
    {
        this.StubJsonOk("/signup");
        await this.client.SignUpAsync(SignUpType.Email, Email, "a-password");
        this.server.VerifySingleReceivedRequestTo("/signup").ReadJsonBodyField("code_challenge").Should().NotBeNullOrEmpty(
            "a PKCE client starts an email sign-up with a code challenge when the call does not choose a flow (RFC 7636 §4.3)");
    }

    [TestMethod]
    public async Task SignInWithSSO_ShouldSendACodeChallenge_GivenAProviderIdAndNoOptions()
    {
        this.StubSsoRedirect();
        await this.client.SignInWithSSO(SsoProviderId);
        this.server.VerifySingleReceivedRequestTo("/sso").ReadJsonBodyField("code_challenge").Should().NotBeNullOrEmpty(
            "a PKCE client starts an SSO sign-in with a code challenge when the call does not choose a flow (RFC 7636 §4.3)");
    }

    [TestMethod]
    public async Task SignInWithSSO_ShouldSendACodeChallenge_GivenADomainAndNoOptions()
    {
        this.StubSsoRedirect();
        await this.client.SignInWithSSO(SsoDomain);
        this.server.VerifySingleReceivedRequestTo("/sso").ReadJsonBodyField("code_challenge").Should().NotBeNullOrEmpty(
            "a PKCE client starts an SSO sign-in with a code challenge when the call does not choose a flow (RFC 7636 §4.3)");
    }

    [TestMethod]
    public async Task LinkIdentity_ShouldReturnAVerifier_GivenOptionsThatLeaveTheFlowTypeUnset()
    {
        this.StubIdentityAuthorization();
        await this.SignInFromStore();
        var state = await this.client.LinkIdentity(Provider.Github, new SignInOptions());
        state.PKCEVerifier.Should().NotBeNullOrEmpty("linking requires PKCE, which a PKCE client supplies when the call does not choose a flow");
    }

    private async Task SignInFromStore()
    {
        var store = SessionPersistenceSubstitute.Tracking();
        await store.SaveSessionAsync(new Session
        {
            AccessToken = "a-user-access-token",
            RefreshToken = "a-refresh-token",
            ExpiresIn = 3600,
            User = new User { Id = "a-user-id" },
        });
        this.client.SetPersistence(store);
        await this.client.LoadSessionAsync();
    }

    private void StubJsonOk(string path) =>
        this.server
            .Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));

    private void StubSsoRedirect() =>
        this.server
            .Given(Request.Create().WithPath("/sso").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"url":"https://idp.example.com/sso/saml?SAMLRequest=a-request"}"""));

    private void StubIdentityAuthorization() =>
        this.server
            .Given(Request.Create().WithPath("/user/identities/authorize").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""{"url":"https://github.com/login/oauth/authorize?client_id=abc"}"""));
}
