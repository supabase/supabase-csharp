#region

using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
    public async Task SignIn_ShouldNotAddStateToAuthUrl_GivenPkceFlow()
    {
        // Provider-side state is owned by the GoTrue server; the SDK must not inject its own or
        // sign-in fails with bad_oauth_state (issue #377).
        var result = await this.client.SignIn(Provider.Github, new SignInOptions { FlowType = OAuthFlowType.PKCE });
        result.Uri.Query.Should().Contain("flow_type=pkce").And.NotContain("state=");
    }

    private void StubJsonOk(string path) =>
        this.server
            .Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{}"));

    private static string S256(string verifier)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
