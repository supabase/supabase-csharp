#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants.AuthState;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.SignInWithPasskey;

/// <summary>
///     Pins how SignInWithPasskeyAsync completes a passkey sign-in: the POST /passkeys/authentication/verify request
///     it puts on the wire, the session it stores, persists and announces, and the failures raised before and after
///     the request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignInWithPasskeyContractTests
{
    private const string VerifyPath = "/passkeys/authentication/verify";
    private const string CurrentAccessToken = "a-current-access-token";
    private const string PasskeyAccessToken = "a-passkey-access-token";
    private static readonly Guid ChallengeId = Guid.Parse("9a6c2f4e-3b1d-4e8a-a7c5-0f2d6b9e1c38");
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
    public async Task SignInWithPasskeyAsync_ShouldPostTheAssertionWithTheApiKey()
    {
        this.MockVerifyResponse(200, Fixture("signed_in_session.json"));
        await this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath(VerifyPath)
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldReturnThePasskeyOwnersSession_GivenAcceptedAssertion()
    {
        this.MockVerifyResponse(200, Fixture("signed_in_session.json"));
        var session = await this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        using (new AssertionScope())
        {
            session.AccessToken.Should().Be(PasskeyAccessToken, "the server answers an AccessTokenResponse for the passkey's owner (supabase/auth PasskeyAuthenticationVerify)");
            session.RefreshToken.Should().Be("a-passkey-refresh-token");
            session.User!.Id.Should().Be("11111111-1111-1111-1111-111111111111");
        }
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldReplaceTheCurrentSession_GivenAcceptedAssertion()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("signed_in_session.json"));
        var session = await this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        this.client.CurrentSession.Should().BeSameAs(session, "a passkey sign-in replaces whoever was signed in, as every sign-in does");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldPersistTheSession_GivenAcceptedAssertion()
    {
        this.MockVerifyResponse(200, Fixture("signed_in_session.json"));
        await this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await this.persistence.LoadSessionAsync())!.AccessToken.Should().Be(PasskeyAccessToken, "the passkey session survives a restart like any other session");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldAnnounceUserUpdatedThenSignedIn_GivenAcceptedAssertion()
    {
        var stateChanges = new List<Constants.AuthState>();
        this.client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        this.MockVerifyResponse(200, Fixture("signed_in_session.json"));
        await this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        stateChanges.Should().Equal(new[] { UserUpdated, SignedIn }, "listeners learn about a passkey sign-in exactly as about an ID token sign-in");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowUnknown_GivenSuccessWithoutBody()
    {
        this.MockVerifyResponse(200, "null");
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await signingIn.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no session for the passkey sign-in.")).Which.Reason.Should().Be(Unknown,
            "a 200 answer must carry the session");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowUnknown_GivenSessionWithoutAccessToken()
    {
        this.MockVerifyResponse(200, Fixture("session_without_access_token.json"));
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(Unknown,
            "a session without an access token cannot stand for the signed-in user");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldKeepTheCurrentSession_GivenSessionWithoutAccessToken()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("session_without_access_token.json"));
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        await signingIn.Should().ThrowAsync<GotrueException>();
        this.client.CurrentSession!.AccessToken.Should().Be(CurrentAccessToken, "an unusable answer must not replace the signed-in session");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldKeepTheCurrentSession_GivenRejectedAssertion()
    {
        this.SignIn();
        this.MockVerifyResponse(400, Fixture("verification_failed_error.json"));
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        await signingIn.Should().ThrowAsync<GotrueException>();
        this.client.CurrentSession!.AccessToken.Should().Be(CurrentAccessToken, "a rejected sign-in leaves whoever was signed in signed in");
    }

    [TestMethod]
    [DataRow("challenge_not_found_error.json", "webauthn_challenge_not_found")]
    [DataRow("challenge_expired_error.json", "webauthn_challenge_expired")]
    [DataRow("verification_failed_error.json", "webauthn_verification_failed")]
    public async Task SignInWithPasskeyAsync_ShouldThrowTheServersErrorCode_GivenRejectedAssertion(string fixture, string errorCode)
    {
        this.MockVerifyResponse(400, Fixture(fixture));
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be(errorCode,
            "the server answers 400 with the WebAuthn error code (supabase/auth PasskeyAuthenticationVerify)");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowUserEmailNotConfirmed_GivenUnconfirmedUser()
    {
        this.MockVerifyResponse(403, Fixture("email_not_confirmed_error.json"));
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(UserEmailNotConfirmed,
            "a passkey cannot sign in a user whose email is unconfirmed (supabase/auth PasskeyAuthenticationVerify)");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowServerRejection_GivenPasskeysDisabled()
    {
        this.MockVerifyResponse(404, Fixture("passkeys_disabled_error.json"));
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion());
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("passkey_disabled",
            "every /passkeys route answers 404 passkey_disabled when passkeys are off on the server (supabase/auth requirePasskeyEnabled)");
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("\"an-assertion\"")]
    [DataRow("null")]
    public async Task SignInWithPasskeyAsync_ShouldThrowArgumentException_GivenAssertionThatIsNotAnObject(string json)
    {
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, JsonDocument.Parse(json).RootElement);
        (await signingIn.Should().ThrowAsync<ArgumentException>().WithMessage("The passkey assertion must be a JSON object.*")).Which.ParamName.Should().Be("credential",
            "a WebAuthn assertion is an AuthenticationResponseJSON object (W3C WebAuthn Level 3)");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowArgumentException_GivenDefaultAssertion()
    {
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, default);
        (await signingIn.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("credential",
            "an uninitialized JsonElement holds no assertion");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldNotSendARequest_GivenAssertionThatIsNotAnObject()
    {
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, default);
        await signingIn.Should().ThrowAsync<ArgumentException>();
        this.server.CountReceivedRequests().Should().Be(0, "argument misuse is detected before the wire");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.MockVerifyResponse(200, Fixture("signed_in_session.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var signingIn = () => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion(), cancellation.Token);
        await signingIn.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void SignInWithPasskeyAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.MockVerifyResponse(200, Fixture("signed_in_session.json"));
        BlockingCaller.Completes(() => this.client.SignInWithPasskeyAsync(ChallengeId, Assertion())).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public void SignInWithPasskeyAsync_ShouldComplete_GivenBlockingCallerAndSessionStoreThatSavesAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously();
        this.persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => SaveLater());
        BlockingCaller.Completes(() => synchronousClient.SignInWithPasskeyAsync(ChallengeId, Assertion())).Should().BeTrue(
            "storing the session must not resume on the caller's context, even when the answer arrived without leaving it");
    }

    [TestMethod]
    public void SignInWithPasskeyAsync_ShouldComplete_GivenBlockingCallerAndSessionStoreThatSavesOnlyTheLastWriteAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously();
        this.persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => Task.CompletedTask, _ => SaveLater());
        BlockingCaller.Completes(() => synchronousClient.SignInWithPasskeyAsync(ChallengeId, Assertion())).Should().BeTrue(
            "announcing the sign-in must not resume on the caller's context, even when every earlier step stayed on it");
    }

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

    private static JsonElement Assertion() => JsonDocument.Parse(Fixture("assertion.json")).RootElement;

    private void MockVerifyResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath(VerifyPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignInWithPasskey", "Data", name));

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
