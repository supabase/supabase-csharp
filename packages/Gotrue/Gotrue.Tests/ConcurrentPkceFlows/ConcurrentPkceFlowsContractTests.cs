#region

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

namespace Gotrue.Tests.ConcurrentPkceFlows;

/// <summary>
///     Pins that PKCE flows started on one client stay independent, against a stubbed server: each flow's start hands
///     back a verifier of its own, and completing a flow sends that flow's verifier even after later flows started.
///     The client stores no verifier, so there is no shared slot a later flow could overwrite.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ConcurrentPkceFlowsContractTests
{
    private const string Email = "user@example.com";
    private const string AuthCode = "an-auth-code";
    private static readonly SignInOptions PkceSignIn = new() { FlowType = OAuthFlowType.PKCE };
    private IGotrueClient<User, Session> client = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignIn_ShouldGiveEachPkceFlowItsOwnVerifier_GivenTwoFlowsStartedBackToBack()
    {
        var first = await this.client.SignIn(Provider.Github, PkceSignIn);
        var second = await this.client.SignIn(Provider.Github, PkceSignIn);
        second.PKCEVerifier.Should().NotBe(first.PKCEVerifier, "each flow is bound to its own challenge, so it needs its own verifier (auth.sign_in.concurrent_pkce_flows)");
    }

    [TestMethod]
    public async Task ExchangeCodeForSession_ShouldSendTheVerifierOfTheFlowItCompletes_GivenAPasswordResetStartedAfterIt()
    {
        this.StubRecoverAndExchange();
        var oauth = await this.client.SignIn(Provider.Github, PkceSignIn);
        await this.client.ResetPasswordForEmail(new ResetPasswordForEmailOptions(Email) { FlowType = OAuthFlowType.PKCE });
        await this.client.ExchangeCodeForSession(oauth.PKCEVerifier!, AuthCode);
        this.server.VerifySingleReceivedRequestTo("/token").ReadJsonBodyField("code_verifier").Should().Be(oauth.PKCEVerifier,
            "a password reset started while an OAuth sign-in is pending must not change the verifier that completes the sign-in (auth.sign_in.concurrent_pkce_flows)");
    }

    private void StubRecoverAndExchange()
    {
        this.server
            .Given(Request.Create().WithPath("/recover").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
        this.server
            .Given(Request.Create().WithPath("/token").WithParam("grant_type", "pkce").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
    }
}
