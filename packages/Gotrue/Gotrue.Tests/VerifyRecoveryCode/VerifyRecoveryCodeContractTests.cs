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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants.AuthState;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.VerifyRecoveryCode;

/// <summary>
///     Pins how VerifyRecoveryCodeAsync redeems an MFA recovery code: the POST /factors/recovery-codes/verify request
///     it puts on the wire, the aal2 session it stores, persists and announces, and the failures raised before and
///     after the request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class VerifyRecoveryCodeContractTests
{
    private const string AccessToken = "an-access-token";
    private const string VerifiedAccessToken = "an-aal2-access-token";
    private const string RecoveryCode = "K4M9-X7QP-2AB8-HT3Z";
    private IGotrueClient<User, Session> client = null!;
    private IGotrueSessionPersistence<Session> persistence = null!;
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
    public async Task VerifyRecoveryCodeAsync_ShouldPostTheCodeWithTheSessionBearer()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("verified_session.json"));
        await this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/factors/recovery-codes/verify")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldReturnTheVerifiedSession_GivenAcceptedCode()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("verified_session.json"));
        var session = await this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        using (new AssertionScope())
        {
            session.AccessToken.Should().Be(VerifiedAccessToken, "the server answers with an aal2 AccessTokenResponse (auth OpenAPI, POST /factors/recovery-codes/verify 200)");
            session.RefreshToken.Should().Be("an-aal2-refresh-token");
            session.User!.Id.Should().Be("11111111-1111-1111-1111-111111111111");
        }
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldReplaceTheCurrentSession_GivenAcceptedCode()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("verified_session.json"));
        var session = await this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        this.client.CurrentSession.Should().BeSameAs(session, "the aal2 session replaces the aal1 one, as a verified TOTP code does");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldPersistTheVerifiedSession_GivenAcceptedCode()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("verified_session.json"));
        await this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await this.persistence.LoadSessionAsync())!.AccessToken.Should().Be(VerifiedAccessToken, "the aal2 session survives a restart like any other session");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldAnnounceUserUpdatedThenMfaChallengeVerified_GivenAcceptedCode()
    {
        this.SignIn();
        var stateChanges = new List<Constants.AuthState>();
        this.client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        this.MockVerifyResponse(200, Fixture("verified_session.json"));
        await this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        stateChanges.Should().Equal(new[] { UserUpdated, MfaChallengeVerified }, "listeners learn about a recovery code exactly as about a verified TOTP code");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowMfaChallengeUnverified_GivenSuccessWithoutBody()
    {
        this.SignIn();
        this.MockVerifyResponse(200, "null");
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>().WithMessage("Could not verify MFA.")).Which.Reason.Should().Be(MfaChallengeUnverified,
            "a 200 answer must carry the aal2 session");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowMfaChallengeUnverified_GivenSessionWithoutAccessToken()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("session_without_access_token.json"));
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(MfaChallengeUnverified,
            "a session without an access token cannot stand for the verified user");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldKeepTheCurrentSession_GivenSessionWithoutAccessToken()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("session_without_access_token.json"));
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        await verifying.Should().ThrowAsync<GotrueException>();
        this.client.CurrentSession!.AccessToken.Should().Be(AccessToken, "an unusable answer must not replace the signed-in session");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowMfaChallengeUnverified_GivenWrongOrUsedCode()
    {
        this.SignIn();
        this.MockVerifyResponse(422, Fixture("verification_failed_error.json"));
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(MfaChallengeUnverified,
            "a wrong, already used or unknown code answers 422 mfa_verification_failed (auth OpenAPI, POST /factors/recovery-codes/verify)");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldKeepTheCurrentSession_GivenHookRejection()
    {
        this.SignIn();
        this.MockVerifyResponse(403, Fixture("verification_rejected_error.json"));
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(MfaChallengeUnverified,
            "the MFA verification attempt hook rejection answers 403 mfa_verification_rejected (auth OpenAPI)");
        this.client.CurrentSession!.AccessToken.Should().Be(AccessToken, "a rejection is surfaced, never acted on locally, as with a TOTP verification");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowUserTooManyRequests_GivenLockedVerification()
    {
        this.SignIn();
        this.MockVerifyResponse(429, Fixture("locked_error.json"));
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.Should().Match<GotrueException>(rejection =>
                rejection.ErrorCode == "mfa_recovery_codes_locked" && rejection.Reason == UserTooManyRequests,
            "too many failed attempts lock verification with 429 mfa_recovery_codes_locked (auth OpenAPI)");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowServerRejection_GivenVerificationDisabled()
    {
        this.SignIn();
        this.MockVerifyResponse(422, Fixture("verify_not_enabled_error.json"));
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_recovery_codes_verify_not_enabled",
            "the server refuses recovery codes when their verification is disabled (auth OpenAPI, 422 mfa_recovery_codes_verify_not_enabled)");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowArgumentException_GivenMissingCode(string? code)
    {
        this.SignIn();
        var verifying = () => this.client.VerifyRecoveryCodeAsync(code!);
        (await verifying.Should().ThrowAsync<ArgumentException>().WithMessage("A recovery code is required.*")).Which.ParamName.Should().Be("code",
            "the server rejects an empty code with validation_failed; the SDK catches it before the wire");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldNotSendARequest_GivenMissingCode()
    {
        this.SignIn();
        var verifying = () => this.client.VerifyRecoveryCodeAsync(" ");
        await verifying.Should().ThrowAsync<ArgumentException>();
        this.server.CountReceivedRequests().Should().Be(0, "argument misuse is detected before the wire");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "POST /factors/recovery-codes/verify requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "POST /factors/recovery-codes/verify requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode);
        await verifying.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("verified_session.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var verifying = () => this.client.VerifyRecoveryCodeAsync(RecoveryCode, cancellation.Token);
        await verifying.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void VerifyRecoveryCodeAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("verified_session.json"));
        BlockingCaller.Completes(() => this.client.VerifyRecoveryCodeAsync(RecoveryCode)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public void VerifyRecoveryCodeAsync_ShouldComplete_GivenBlockingCallerAndSessionStoreThatSavesAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously();
        this.SignIn(synchronousClient);
        this.persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => SaveLater());
        BlockingCaller.Completes(() => synchronousClient.VerifyRecoveryCodeAsync(RecoveryCode)).Should().BeTrue(
            "storing the session must not resume on the caller's context, even when the answer arrived without leaving it");
    }

    [TestMethod]
    public void VerifyRecoveryCodeAsync_ShouldComplete_GivenBlockingCallerAndSessionStoreThatSavesOnlyTheLastWriteAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously();
        this.SignIn(synchronousClient);
        this.persistence.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => Task.CompletedTask, _ => SaveLater());
        BlockingCaller.Completes(() => synchronousClient.VerifyRecoveryCodeAsync(RecoveryCode)).Should().BeTrue(
            "announcing the verification must not resume on the caller's context, even when every earlier step stayed on it");
    }

    private void SignIn() => this.SignIn(this.client);

    private void SignIn(Session session) => this.SignIn(this.client, session);

    private void SignIn(IGotrueClient<User, Session> signingIn) =>
        this.SignIn(signingIn, new Session { AccessToken = AccessToken, RefreshToken = "a-refresh-token", ExpiresIn = 3600, User = new User { Id = "user-id-123" } });

    private void SignIn(IGotrueClient<User, Session> signingIn, Session session)
    {
        this.persistence = SessionPersistenceSubstitute.Tracking();
        this.persistence.SaveSession(session);
        signingIn.SetPersistence(this.persistence);
        signingIn.LoadSession();
    }

    private IGotrueClient<User, Session> ClientAnsweringSynchronously() =>
        TestClients.Against(this.server, httpClient: new HttpClient(new SynchronousVerificationHandler(Fixture("verified_session.json"))));

    private static async Task SaveLater() => await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

    private void MockVerifyResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/factors/recovery-codes/verify").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VerifyRecoveryCode", "Data", name));

    /// <summary>
    ///     Answers every request with the verified session without yielding, so the SDK continues on the caller's
    ///     thread and context after the request, as a cached or in-process transport would let it.
    /// </summary>
    private sealed class SynchronousVerificationHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
