#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.SignInWithWeb3;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants.AuthState;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.SignInWithWeb3;

/// <summary>
///     Pins how SignInWithWeb3Async signs a wallet in: the POST /token?grant_type=web3 request it puts on the wire, the
///     session it stores, persists and announces, and the server's rejections it surfaces, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignInWithWeb3ContractTests
{
    private const string TokenPath = "/token";
    private const string CurrentAccessToken = "a-current-access-token";
    private const string Web3AccessToken = "a-web3-access-token";
    private const string Message = "localhost:3000 wants you to sign in with your Ethereum account:\n0x2c7536E3605D9C16a7a3D7b1898e529396a65c23";
    private static readonly string Signature = "0x" + new string('a', 130);
    private IGotrueClient<User, Session> client = null!;
    private IGotrueSessionPersistence<Session> persistence = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        this.persistence = SessionPersistenceSubstitute.Tracking();
        this.client.SetPersistence(this.persistence);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldPostTheWeb3GrantWithTheApiKey()
    {
        this.MockTokenResponse(200, Fixture("signed_in_session.json"));
        await this.client.SignInWithWeb3Async(Credentials());
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath(TokenPath)
            .WithQueryParam("grant_type", "web3")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldReturnTheWalletsSession_GivenAcceptedSignature()
    {
        this.MockTokenResponse(200, Fixture("signed_in_session.json"));
        var session = await this.client.SignInWithWeb3Async(Credentials());
        using (new AssertionScope())
        {
            session.AccessToken.Should().Be(Web3AccessToken, "the server answers an AccessTokenResponse for the wallet's user (supabase/auth Web3Grant)");
            session.RefreshToken.Should().Be("a-web3-refresh-token");
            session.User!.Id.Should().Be("22222222-2222-2222-2222-222222222222");
        }
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldReplaceTheCurrentSession_GivenAcceptedSignature()
    {
        this.SignIn();
        this.MockTokenResponse(200, Fixture("signed_in_session.json"));
        var session = await this.client.SignInWithWeb3Async(Credentials());
        this.client.CurrentSession.Should().BeSameAs(session, "a Web3 sign-in replaces whoever was signed in, as every sign-in does");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldPersistTheSession_GivenAcceptedSignature()
    {
        this.MockTokenResponse(200, Fixture("signed_in_session.json"));
        await this.client.SignInWithWeb3Async(Credentials());
        (await this.persistence.LoadSessionAsync())!.AccessToken.Should().Be(Web3AccessToken, "the Web3 session survives a restart like any other session");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldAnnounceUserUpdatedThenSignedIn_GivenAcceptedSignature()
    {
        var stateChanges = new List<Constants.AuthState>();
        this.client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        this.MockTokenResponse(200, Fixture("signed_in_session.json"));
        await this.client.SignInWithWeb3Async(Credentials());
        stateChanges.Should().Equal(new[] { UserUpdated, SignedIn }, "listeners learn about a Web3 sign-in exactly as about a passkey sign-in");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldThrowUnknown_GivenSuccessWithoutBody()
    {
        this.MockTokenResponse(200, "null");
        var signingIn = () => this.client.SignInWithWeb3Async(Credentials());
        (await signingIn.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no session for the Web3 sign-in.")).Which.Reason.Should().Be(Unknown,
            "a 200 answer must carry the session");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldKeepTheCurrentSession_GivenSessionWithoutAccessToken()
    {
        this.SignIn();
        this.MockTokenResponse(200, Fixture("session_without_access_token.json"));
        var signingIn = () => this.client.SignInWithWeb3Async(Credentials());
        await signingIn.Should().ThrowAsync<GotrueException>();
        this.client.CurrentSession!.AccessToken.Should().Be(CurrentAccessToken, "an unusable answer must not replace the signed-in session");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldKeepTheCurrentSession_GivenRejectedSignature()
    {
        this.SignIn();
        this.MockTokenResponse(400, Fixture("signature_mismatch_error.json"));
        var signingIn = () => this.client.SignInWithWeb3Async(Credentials());
        await signingIn.Should().ThrowAsync<GotrueException>();
        this.client.CurrentSession!.AccessToken.Should().Be(CurrentAccessToken, "a rejected sign-in leaves whoever was signed in signed in");
    }

    [TestMethod]
    [DataRow(422, "web3_disabled_error.json", "web3_provider_disabled")]
    [DataRow(400, "validation_failed_error.json", "validation_failed")]
    [DataRow(400, "captcha_failed_error.json", "captcha_failed")]
    public async Task SignInWithWeb3Async_ShouldThrowTheServersErrorCode_GivenRejectedRequest(int statusCode, string fixture, string errorCode)
    {
        this.MockTokenResponse(statusCode, Fixture(fixture));
        var signingIn = () => this.client.SignInWithWeb3Async(Credentials());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be(errorCode,
            "the server names why it refused the Web3 grant (supabase/auth Web3Grant)");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldThrowTheServersDescription_GivenSignatureOfAnotherWallet()
    {
        this.MockTokenResponse(400, Fixture("signature_mismatch_error.json"));
        var signingIn = () => this.client.SignInWithWeb3Async(Credentials());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.Message.Should().Contain("Signature does not match address in message",
            "the server answers an OAuth invalid_grant error, without an error code, when the signer is not the message's address (supabase/auth web3GrantEthereum)");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldThrowUserTooManyRequests_GivenRateLimitReached()
    {
        this.MockTokenResponse(429, Fixture("rate_limited_error.json"));
        var signingIn = () => this.client.SignInWithWeb3Async(Credentials());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(UserTooManyRequests,
            "Web3 grants have their own rate limit (supabase/auth Token, limiterOpts.Web3)");
    }

    [TestMethod]
    public async Task SignInWithWeb3Async_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.MockTokenResponse(200, Fixture("signed_in_session.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var signingIn = () => this.client.SignInWithWeb3Async(Credentials(), cancellationToken: cancellation.Token);
        await signingIn.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void SignInWithWeb3Async_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.MockTokenResponse(200, Fixture("signed_in_session.json"));
        BlockingCaller.Completes(() => this.client.SignInWithWeb3Async(Credentials())).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public void SignInWithWeb3Async_ShouldComplete_GivenBlockingCallerAndSessionStoreThatSavesAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously();
        this.persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => SaveLater());
        BlockingCaller.Completes(() => synchronousClient.SignInWithWeb3Async(Credentials())).Should().BeTrue(
            "storing the session must not resume on the caller's context, even when the answer arrived without leaving it");
    }

    [TestMethod]
    public void SignInWithWeb3Async_ShouldComplete_GivenBlockingCallerAndSessionStoreThatSavesOnlyTheLastWriteAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously();
        this.persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => Task.CompletedTask, _ => SaveLater());
        BlockingCaller.Completes(() => synchronousClient.SignInWithWeb3Async(Credentials())).Should().BeTrue(
            "announcing the sign-in must not resume on the caller's context, even when every earlier step stayed on it");
    }

    private static Web3Credentials Credentials() => Web3Credentials.ForEthereum(Message, Signature);

    private void SignIn()
    {
        this.persistence.SaveSession(new Session { AccessToken = CurrentAccessToken, RefreshToken = "a-refresh-token", ExpiresIn = 3600, User = new User { Id = "user-id-123" } });
        this.client.LoadSession();
    }

    private IGotrueClient<User, Session> ClientAnsweringSynchronously()
    {
        var synchronousClient = TestClients.Against(this.server, httpClient: new HttpClient(new SynchronousSignInHandler(Fixture("signed_in_session.json"))));
        synchronousClient.SetPersistence(this.persistence);
        return synchronousClient;
    }

    private static async Task SaveLater() => await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

    private void MockTokenResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath(TokenPath).WithParam("grant_type", "web3").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignInWithWeb3", "Data", name));

    /// <summary>
    ///     Answers every request with the signed-in session without yielding, so the SDK continues on the caller's
    ///     thread and context after the request, as a cached or in-process transport would let it.
    /// </summary>
    private sealed class SynchronousSignInHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
