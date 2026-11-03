#region

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.RegisterPasskey;

/// <summary>
///     Pins how StartPasskeyRegistrationAsync asks for a passkey registration challenge: the
///     POST /passkeys/registration/options request it puts on the wire with the session bearer, how the challenge maps
///     to the result with its WebAuthn options passed through untouched, and the failures raised before and after the
///     request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StartPasskeyRegistrationContractTests
{
    private const string AccessToken = "an-access-token";
    private const string ChallengePath = "/passkeys/registration/options";
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
    public async Task StartPasskeyRegistrationAsync_ShouldPostToTheChallengeEndpointWithTheSessionBearer()
    {
        this.SignIn();
        this.MockChallengeResponse(200, Fixture("registration_challenge.json"));
        await this.client.StartPasskeyRegistrationAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath(ChallengePath)
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldReturnTheChallengeWithItsOptionsVerbatim()
    {
        this.SignIn();
        this.MockChallengeResponse(200, Fixture("registration_challenge.json"));
        var challenge = await this.client.StartPasskeyRegistrationAsync();
        new { challenge.Id, challenge.ExpiresAt, Options = challenge.Options.GetRawText() }.Should().Be(new
        {
            Id = Guid.Parse("4d8e1b7a-6c2f-4a9e-b3d5-7f0c1e2a9b64"),
            ExpiresAt = new DateTimeOffset(2026, 11, 3, 14, 50, 0, TimeSpan.Zero),
            Options = IssuedOptions(),
        },
            "the server answers {challenge_id, options, expires_at in unix seconds}, and the options are W3C JSON for the platform's WebAuthn API (supabase/auth PasskeyRegistrationOptions)");
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldThrow_GivenSuccessWithoutAChallenge()
    {
        this.SignIn();
        this.MockChallengeResponse(200, "null");
        var starting = () => this.client.StartPasskeyRegistrationAsync();
        await starting.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no passkey registration challenge.",
            "a successful answer always carries the challenge, so its absence is a server failure");
    }

    [TestMethod]
    [DataRow(403, "anonymous_error.json", "no_authorization")]
    [DataRow(422, "sso_user_error.json", "validation_failed")]
    [DataRow(403, "insufficient_aal_error.json", "insufficient_aal")]
    [DataRow(422, "too_many_passkeys_error.json", "too_many_passkeys")]
    [DataRow(404, "passkeys_disabled_error.json", "passkey_disabled")]
    public async Task StartPasskeyRegistrationAsync_ShouldThrowTheServersErrorCode_GivenUserWhoCannotRegister(int statusCode, string fixture, string errorCode)
    {
        this.SignIn();
        this.MockChallengeResponse(statusCode, Fixture(fixture));
        var starting = () => this.client.StartPasskeyRegistrationAsync();
        (await starting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be(errorCode,
            "anonymous and SSO users, MFA users below aal2, users at the passkey limit, and every user when passkeys are off are refused a challenge (supabase/auth PasskeyRegistrationOptions)");
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var starting = () => this.client.StartPasskeyRegistrationAsync();
        (await starting.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "POST /passkeys/registration/options requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var starting = () => this.client.StartPasskeyRegistrationAsync();
        (await starting.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "POST /passkeys/registration/options requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var starting = () => this.client.StartPasskeyRegistrationAsync();
        await starting.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockChallengeResponse(200, Fixture("registration_challenge.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var starting = () => this.client.StartPasskeyRegistrationAsync(cancellation.Token);
        await starting.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void StartPasskeyRegistrationAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockChallengeResponse(200, Fixture("registration_challenge.json"));
        BlockingCaller.Completes(() => this.client.StartPasskeyRegistrationAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    private static string IssuedOptions()
    {
        using var issued = JsonDocument.Parse(Fixture("registration_challenge.json"));
        return issued.RootElement.GetProperty("options").GetRawText();
    }

    private void SignIn() => this.SignIn(new Session { AccessToken = AccessToken, RefreshToken = "a-refresh-token", ExpiresIn = 3600, User = new User { Id = "user-id-123" } });

    private void SignIn(Session session)
    {
        var persistence = SessionPersistenceSubstitute.Tracking();
        persistence.SaveSession(session);
        this.client.SetPersistence(persistence);
        this.client.LoadSession();
    }

    private void MockChallengeResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath(ChallengePath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RegisterPasskey", "Data", name));
}
