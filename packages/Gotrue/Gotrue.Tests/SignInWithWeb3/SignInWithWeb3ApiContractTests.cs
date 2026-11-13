#region

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.SignInWithWeb3;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     Pins that the stateless SignInWithWeb3Async twin on the API never hands back an unusable session: an answer
///     without a session, or with a session missing its access token, fails instead of returning, all against a stubbed
///     server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignInWithWeb3ApiContractTests
{
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
    public async Task SignInWithWeb3Async_ShouldReturnTheWalletsSession_GivenAcceptedSignature()
    {
        this.MockTokenResponse(Fixture("signed_in_session.json"));
        (await this.api.SignInWithWeb3Async(Credentials())).AccessToken.Should().Be("a-web3-access-token",
            "the twin hands the caller the session the server issued");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldThrowUnknown_GivenSuccessWithoutBody()
    {
        this.MockTokenResponse("null");
        var signingIn = () => this.api.SignInWithWeb3Async(Credentials());
        (await signingIn.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no session for the Web3 sign-in.")).Which.Reason.Should().Be(Unknown,
            "a 200 answer must carry the session, so the twin returns a session or fails");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldThrowUnknown_GivenSessionWithoutAccessToken()
    {
        this.MockTokenResponse(Fixture("session_without_access_token.json"));
        var signingIn = () => this.api.SignInWithWeb3Async(Credentials());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(Unknown,
            "a session without an access token cannot stand for the signed-in user");
    }

    private static Web3Credentials Credentials() => Web3Credentials.ForSolana("a-sign-in-message", new byte[64]);

    private void MockTokenResponse(string body) =>
        this.server
            .Given(Request.Create().WithPath("/token").WithParam("grant_type", "web3").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignInWithWeb3", "Data", name));
}
