#region

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.Resend;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.Pkce;

/// <summary>
///     Pins the PKCE wire contract for the flows that use it: the SDK keeps the raw code verifier and sends
///     only its SHA-256 code challenge to the server, and the OAuth authorize URL carries no client `state`
///     (that is generated and validated by the GoTrue server). Asserted against a stubbed server (RFC 7636).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class PkceContractTests
{
    private const string CallerVerifier = "a-caller-supplied-verifier";
    private const string SsoDomain = "example.com";
    private static readonly Guid SsoProviderId = Guid.Parse("4c9f2a1e-7b3d-4e6a-9f8c-1d2e3b4a5c6d");
    private static readonly ResendParameters SignUpByEmail = new(ResendType.SignUp, "test@example.com", null, null);

    private IGotrueClient<User, Session> client = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignInWithOtp_ShouldSendSha256OfVerifierAsCodeChallenge_GivenPkceFlow()
    {
        this.StubJsonOk("/otp");
        var state = await this.client.SignInWithOtp(new SignInWithPasswordlessEmailOptions("test@example.com")
        {
            FlowType = OAuthFlowType.PKCE,
        });
        var request = this.server.VerifySingleReceivedRequest().WithMethod(HttpMethod.Post).WithPath("/otp");
        request.ReadJsonBodyField("code_challenge_method").Should().Be("s256");
        request.ReadJsonBodyField("code_challenge").Should().Be(S256(state.PKCEVerifier!),
            "the server must receive BASE64URL(SHA256(verifier)) per RFC 7636 §4.2");
    }

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldSendSha256OfVerifierAsCodeChallenge_GivenPkceFlow()
    {
        this.StubJsonOk("/recover");
        var state = await this.client.ResetPasswordForEmail(new ResetPasswordForEmailOptions("test@example.com")
        {
            FlowType = OAuthFlowType.PKCE,
        });
        var request = this.server.VerifySingleReceivedRequest().WithMethod(HttpMethod.Post).WithPath("/recover");
        request.ReadJsonBodyField("code_challenge_method").Should().Be("s256");
        request.ReadJsonBodyField("code_challenge").Should().Be(S256(state.PKCEVerifier!),
            "the server must receive BASE64URL(SHA256(verifier)) per RFC 7636 §4.2");
    }

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldGenerateItsOwnVerifier_GivenDeprecatedOptionsVerifier()
    {
        this.StubJsonOk("/recover");
        var options = new ResetPasswordForEmailOptions("test@example.com") { FlowType = OAuthFlowType.PKCE };
        typeof(ResetPasswordForEmailOptions).GetProperty("PKCEVerifier")!.SetValue(options, CallerVerifier);
        var state = await this.client.ResetPasswordForEmail(options);
        state.PKCEVerifier.Should().NotBe(CallerVerifier,
            "the options verifier is obsolete and never read: the SDK generates the verifier and returns it on the state");
    }

    [TestMethod]
    public async Task Resend_ShouldSendSha256OfVerifierAsCodeChallenge_GivenPkceFlow()
    {
        this.StubJsonOk("/resend");
        var state = await this.client.Resend(SignUpByEmail, OAuthFlowType.PKCE);
        var request = this.server.VerifySingleReceivedRequest().WithMethod(HttpMethod.Post).WithPath("/resend");
        request.ReadJsonBodyField("code_challenge_method").Should().Be("s256");
        request.ReadJsonBodyField("code_challenge").Should().Be(S256(state.PKCEVerifier!),
            "the server must receive BASE64URL(SHA256(verifier)) per RFC 7636 section 4.2");
    }

    [TestMethod]
    public async Task Resend_ShouldNotSendCodeChallenge_GivenImplicitFlow()
    {
        this.StubJsonOk("/resend");
        var state = await this.client.Resend(SignUpByEmail, OAuthFlowType.Implicit);
        this.server.VerifySingleReceivedRequest().WithoutJsonBodyField("code_challenge");
        state.PKCEVerifier.Should().BeNull();
    }

    [TestMethod]
    public async Task Resend_ShouldNotSendCodeChallenge_GivenPhoneChangeOnPkceFlow()
    {
        this.StubJsonOk("/resend");
        var parameters = new ResendParameters(ResendType.PhoneChange, null, "+15555550100", null);
        var state = await this.client.Resend(parameters, OAuthFlowType.PKCE);
        this.server.VerifySingleReceivedRequest().WithoutJsonBodyField("code_challenge");
        state.PKCEVerifier.Should().BeNull();
    }

    [TestMethod]
    public async Task SignInWithSSO_ShouldReturnTheVerifierOfTheChallengeItSent_GivenPkceFlow()
    {
        this.StubSsoRedirect();
        var state = await this.client.SignInWithSSO(SsoProviderId, new SignInWithSSOOptions { FlowType = OAuthFlowType.PKCE });
        var request = this.server.VerifySingleReceivedRequest().WithMethod(HttpMethod.Post).WithPath("/sso");
        request.ReadJsonBodyField("code_challenge").Should().Be(S256(state!.PKCEVerifier!),
            "the caller needs the verifier behind the challenge to exchange the code the SSO callback returns (RFC 7636 §4.5)");
    }

    [TestMethod]
    public async Task SignInWithSSO_ShouldReturnNoVerifier_GivenImplicitFlow()
    {
        this.StubSsoRedirect();
        var state = await this.client.SignInWithSSO(SsoDomain);
        state!.PKCEVerifier.Should().BeNull("an implicit flow sends no challenge, so there is no verifier to keep");
    }

    [TestMethod]
    public async Task SignInWithSSO_ShouldReturnNull_GivenSuccessWithoutBody()
    {
        this.server
            .Given(Request.Create().WithPath("/sso").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("null"));
        (await this.client.SignInWithSSO(SsoDomain, new SignInWithSSOOptions { FlowType = OAuthFlowType.PKCE })).Should().BeNull(
            "an answer without a redirect URL leaves nothing to hand the verifier back on");
    }

    [TestMethod]
    public void SignInWithSSO_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.StubSsoRedirect();
        BlockingCaller.Completes(() => this.client.SignInWithSSO(SsoDomain, new SignInWithSSOOptions { FlowType = OAuthFlowType.PKCE })).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public void Resend_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.StubJsonOk("/resend");
        BlockingCaller.Completes(() => this.client.Resend(SignUpByEmail, OAuthFlowType.PKCE)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public void ExchangeCodeForSession_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.StubTokenExchange();
        BlockingCaller.Completes(() => this.client.ExchangeCodeForSession("code-verifier", "auth-code")).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task SignIn_ShouldNotAddStateToAuthUrl_GivenPkceFlow()
    {
        // Provider-side state is owned by the GoTrue server; the SDK must not inject its own or
        // sign-in fails with bad_oauth_state (issue #377).
        var result = await this.client.SignIn(Provider.Github, new SignInOptions { FlowType = OAuthFlowType.PKCE });
        result.Uri.Query.Should().Contain("flow_type=pkce").And.NotContain("state=");
    }

    private void StubSsoRedirect() =>
        this.server
            .Given(Request.Create().WithPath("/sso").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"url":"https://idp.example.com/sso/saml?SAMLRequest=a-request"}"""));

    private void StubJsonOk(string path) =>
        this.server
            .Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{}"));

    private void StubTokenExchange() =>
        this.server
            .Given(Request.Create().WithPath("/token").WithParam("grant_type", "pkce").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(Fixture("exchanged_session.json")));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pkce", "Data", name));

    private static string S256(string verifier)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
