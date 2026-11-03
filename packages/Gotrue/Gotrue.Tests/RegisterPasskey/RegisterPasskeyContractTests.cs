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
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.RegisterPasskey;

/// <summary>
///     Pins how RegisterPasskeyAsync completes a passkey registration: the POST /passkeys/registration/verify request it
///     puts on the wire with the session bearer, how the registered passkey maps to the result, and the failures raised
///     before and after the request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RegisterPasskeyContractTests
{
    private const string AccessToken = "an-access-token";
    private const string VerifyPath = "/passkeys/registration/verify";
    private static readonly Guid ChallengeId = Guid.Parse("4d8e1b7a-6c2f-4a9e-b3d5-7f0c1e2a9b64");
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
    public async Task RegisterPasskeyAsync_ShouldPostTheCredentialWithTheSessionBearer()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("registered_passkey.json"));
        await this.client.RegisterPasskeyAsync(ChallengeId, Credential());
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath(VerifyPath)
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldReturnTheRegisteredPasskey_GivenAcceptedCredential()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("registered_passkey.json"));
        var passkey = await this.client.RegisterPasskeyAsync(ChallengeId, Credential());
        passkey.Should().Be(new Passkey
        {
            Id = Guid.Parse("5f0b6c1e-8a43-4d1f-9c7e-2b8f3e6a1d24"),
            FriendlyName = "iCloud Keychain",
            CreatedAt = new DateTimeOffset(2026, 11, 3, 14, 45, 12, TimeSpan.Zero).AddTicks(6543210),
        }, "the server answers {id, friendly_name, created_at} for the new passkey, which has never been used (supabase/auth PasskeyMetadataResponse)");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldThrow_GivenSuccessWithoutAPasskey()
    {
        this.SignIn();
        this.MockVerifyResponse(200, "null");
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, Credential());
        await registering.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no passkey.",
            "a registration always answers with the new passkey, so its absence is a server failure");
    }

    [TestMethod]
    [DataRow(400, "challenge_not_found_error.json", "webauthn_challenge_not_found")]
    [DataRow(400, "challenge_expired_error.json", "webauthn_challenge_expired")]
    [DataRow(400, "verification_failed_error.json", "webauthn_verification_failed")]
    [DataRow(422, "credential_exists_error.json", "webauthn_credential_exists")]
    [DataRow(422, "too_many_passkeys_error.json", "too_many_passkeys")]
    [DataRow(403, "insufficient_aal_error.json", "insufficient_aal")]
    [DataRow(404, "passkeys_disabled_error.json", "passkey_disabled")]
    public async Task RegisterPasskeyAsync_ShouldThrowTheServersErrorCode_GivenRejectedCredential(int statusCode, string fixture, string errorCode)
    {
        this.SignIn();
        this.MockVerifyResponse(statusCode, Fixture(fixture));
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, Credential());
        (await registering.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be(errorCode,
            "the server answers with the WebAuthn or passkey error code (supabase/auth PasskeyRegistrationVerify)");
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("\"a-credential\"")]
    [DataRow("null")]
    public async Task RegisterPasskeyAsync_ShouldThrowArgumentException_GivenCredentialThatIsNotAnObject(string json)
    {
        this.SignIn();
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, JsonDocument.Parse(json).RootElement);
        (await registering.Should().ThrowAsync<ArgumentException>().WithMessage("The passkey credential must be a JSON object.*")).Which.ParamName.Should().Be("credential",
            "a WebAuthn registration is a RegistrationResponseJSON object (W3C WebAuthn Level 3)");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldThrowArgumentException_GivenDefaultCredential()
    {
        this.SignIn();
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, default);
        (await registering.Should().ThrowAsync<ArgumentException>()).Which.ParamName.Should().Be("credential",
            "an uninitialized JsonElement holds no credential");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldNotSendARequest_GivenCredentialThatIsNotAnObject()
    {
        this.SignIn();
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, default);
        await registering.Should().ThrowAsync<ArgumentException>();
        this.server.CountReceivedRequests().Should().Be(0, "argument misuse is detected before the wire");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, Credential());
        (await registering.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "POST /passkeys/registration/verify requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, Credential());
        (await registering.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "POST /passkeys/registration/verify requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, Credential());
        await registering.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("registered_passkey.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var registering = () => this.client.RegisterPasskeyAsync(ChallengeId, Credential(), cancellation.Token);
        await registering.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void RegisterPasskeyAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockVerifyResponse(200, Fixture("registered_passkey.json"));
        BlockingCaller.Completes(() => this.client.RegisterPasskeyAsync(ChallengeId, Credential())).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    private static JsonElement Credential() => JsonDocument.Parse(Fixture("credential.json")).RootElement;

    private void SignIn() => this.SignIn(new Session { AccessToken = AccessToken, RefreshToken = "a-refresh-token", ExpiresIn = 3600, User = new User { Id = "user-id-123" } });

    private void SignIn(Session session)
    {
        var persistence = SessionPersistenceSubstitute.Tracking();
        persistence.SaveSession(session);
        this.client.SetPersistence(persistence);
        this.client.LoadSession();
    }

    private void MockVerifyResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath(VerifyPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RegisterPasskey", "Data", name));
}
