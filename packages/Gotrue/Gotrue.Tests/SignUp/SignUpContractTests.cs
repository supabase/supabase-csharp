#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
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
using Supabase.Gotrue.SignUp;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.SignUp;

/// <summary>
///     Pins what SignUpAsync hands back and adopts: a session the server issued is signed in, persisted and announced;
///     a user awaiting confirmation is returned without signing in; an email sign-up on the PKCE flow keeps the
///     verifier of the challenge it sent; a phone sign-up sends its messaging channel.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignUpContractTests
{
    private const string Spec = "spec auth.sign_in.sign_up";
    private const int UnknownSignUpType = 99;
    private static readonly TimeSpan SlowSave = TimeSpan.FromMilliseconds(50);
    private readonly List<AuthState> stateChanges = new();
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
        this.client.AddStateChangedListener((_, state) => this.stateChanges.Add(state));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignUpAsync_ShouldSignTheUserIn_GivenTheServerIssuedASession()
    {
        this.StubSignUp("auto-confirmed-sign-up.json");
        var signedUp = await this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password);
        using (new AssertionScope())
        {
            signedUp.Session.Should().BeSameAs(this.client.CurrentSession, "a session-returning sign-up signs the user in (" + Spec + ")");
            signedUp.Session!.AccessToken.Should().Be("auto-confirmed-access-token");
            signedUp.User!.Id.Should().Be("user-id-auto-confirmed");
            this.persistence.LoadSession().Should().BeSameAs(this.client.CurrentSession, "the SDK must persist the session (" + Spec + ")");
            this.stateChanges.Should().Contain(SignedIn, "the SDK must emit a sign-in event (" + Spec + ")");
        }
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldReturnTheUserWithoutSigningIn_GivenConfirmationRequired()
    {
        this.StubSignUp("pending-sign-up.json");
        var signedUp = await this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password);
        using (new AssertionScope())
        {
            signedUp.User!.Id.Should().Be("user-id-pending");
            signedUp.Session.Should().BeNull("no session is issued until the user confirms (" + Spec + ")");
            this.client.CurrentSession.Should().BeNull("the SDK must not treat a pending sign-up as a sign-in (" + Spec + ")");
            this.stateChanges.Should().BeEmpty();
        }
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldSignThePendingUserIn_GivenUnconfirmedSessionsAllowed()
    {
        this.StubSignUp("pending-sign-up.json");
        this.client.Options.AllowUnconfirmedUserSessions = true;
        var signedUp = await this.client.SignUpAsync(SignUpType.Phone, "+15555550123", Password);
        using (new AssertionScope())
        {
            signedUp.Session.Should().BeSameAs(this.client.CurrentSession, "the opt-in adopts the pending user in a session without tokens");
            signedUp.Session!.User!.Id.Should().Be("user-id-pending");
            this.stateChanges.Should().Contain(SignedIn);
        }
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldSignInWithTheIssuedSession_GivenUnconfirmedSessionsAllowed()
    {
        this.StubSignUp("auto-confirmed-sign-up.json");
        this.client.Options.AllowUnconfirmedUserSessions = true;
        var signedUp = await this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password);
        signedUp.Session!.AccessToken.Should().Be("auto-confirmed-access-token",
            "the opt-in only matters when the server issued no session (" + Spec + ")");
    }

    [TestMethod]
    public void SignUpAsync_ShouldComplete_GivenACallerBlockingOnItsContext()
    {
        this.StubSignUp("auto-confirmed-sign-up.json");
        var slowStore = SessionPersistenceSubstitute.Tracking();
        slowStore.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => Task.Delay(SlowSave));
        this.client.SetPersistence(slowStore);
        BlockingCaller.Completes(() => this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password)).Should().BeTrue(
            "a library must not resume on the caller's synchronization context (CONVENTIONS §4)");
    }

    [TestMethod]
    public void SignUpAsync_ShouldComplete_GivenABlockingCallerAndAStoreThatSavesAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously(store => store.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => SaveLater()));
        BlockingCaller.Completes(() => synchronousClient.SignUpAsync(SignUpType.Email, RandomEmail(), Password)).Should().BeTrue(
            "storing the session must not resume on the caller's context, even when the answer arrived without leaving it");
    }

    [TestMethod]
    public void SignUpAsync_ShouldComplete_GivenABlockingCallerAndAStoreThatSavesOnlyTheLastWriteAsynchronously()
    {
        var synchronousClient = this.ClientAnsweringSynchronously(store =>
            store.SaveSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()).Returns(_ => Task.CompletedTask, _ => SaveLater()));
        BlockingCaller.Completes(() => synchronousClient.SignUpAsync(SignUpType.Email, RandomEmail(), Password)).Should().BeTrue(
            "announcing the sign-in must not resume on the caller's context, even when every earlier step stayed on it");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldReturnNoUser_GivenAnAcknowledgementOnly()
    {
        this.StubSignUp("acknowledgement.json");
        var signedUp = await this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password);
        signedUp.Should().Be(new SignUpState(), "a body with no id is neither a user nor a session (issue #259)");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldSendTheChallengeOfTheReturnedVerifier_GivenEmailAndThePkceFlow()
    {
        this.StubSignUp("pending-sign-up.json");
        var signedUp = await this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password, new SignUpOptions { FlowType = OAuthFlowType.PKCE });
        var request = this.server.VerifySingleReceivedRequest().WithMethod(HttpMethod.Post).WithPath("/signup");
        using (new AssertionScope())
        {
            request.ReadJsonBodyField("code_challenge_method").Should().Be("s256");
            request.ReadJsonBodyField("code_challenge").Should().Be(S256(signedUp.PKCEVerifier!),
                "the server must receive BASE64URL(SHA256(verifier)) per RFC 7636 §4.2 (" + Spec + ", PKCE)");
        }
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldNotSendAChallenge_GivenPhoneAndThePkceFlow()
    {
        this.StubSignUp("pending-sign-up.json");
        var signedUp = await this.client.SignUpAsync(SignUpType.Phone, "+15555550123", Password, new SignUpOptions { FlowType = OAuthFlowType.PKCE });
        using (new AssertionScope())
        {
            signedUp.PKCEVerifier.Should().BeNull("PKCE is supported for email registration only (" + Spec + ", PKCE)");
            this.server.VerifySingleReceivedRequest().WithoutJsonBodyField("code_challenge");
        }
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldSendTheRedirect_GivenARedirectTo()
    {
        this.StubSignUp("pending-sign-up.json");
        await this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password, new SignUpOptions { RedirectTo = "https://app.example/confirm" });
        this.server.VerifySingleReceivedRequest().WithQueryParam("redirect_to", "https://app.example/confirm");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldThrowUserBadPhoneNumber_GivenAnEmptyPhoneNumber()
    {
        var signUp = () => this.client.SignUpAsync(SignUpType.Phone, string.Empty, Password);
        await signUp.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.UserBadPhoneNumber);
        this.server.CountReceivedRequests().Should().Be(0, "an empty phone number is rejected before the request");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldThrowArgumentOutOfRange_GivenAnUnknownSignUpType()
    {
        var signUp = () => this.client.SignUpAsync((SignUpType) UnknownSignUpType, RandomEmail(), Password);
        await signUp.Should().ThrowAsync<ArgumentOutOfRangeException>("only email and phone credentials can sign up (" + Spec + ")");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldCancelTheRequest_GivenACancelledToken()
    {
        this.StubSignUp("pending-sign-up.json");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var signUp = () => this.client.SignUpAsync(SignUpType.Email, RandomEmail(), Password, cancellationToken: cancelled.Token);
        await signUp.Should().ThrowAsync<OperationCanceledException>("the token reaches the HTTP call");
    }

    private void StubSignUp(string fixture) =>
        this.server.Given(Request.Create().WithPath("/signup").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(Fixture(fixture)));

    private IGotrueClient<User, Session> ClientAnsweringSynchronously(Action<IGotrueSessionPersistence<Session>> configureStore)
    {
        var synchronousClient = TestClients.Against(this.server, httpClient: new HttpClient(new SynchronousSignUpHandler(Fixture("auto-confirmed-sign-up.json"))));
        var store = SessionPersistenceSubstitute.Tracking();
        configureStore(store);
        synchronousClient.SetPersistence(store);
        return synchronousClient;
    }

    private static async Task SaveLater() => await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignUp", "Data", name));

    private static string S256(string verifier)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Answers the sign-up without yielding, so the SDK continues on the caller's thread and context.</summary>
    private sealed class SynchronousSignUpHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
