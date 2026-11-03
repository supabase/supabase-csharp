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

namespace Gotrue.Tests.SignInWithPasskey;

/// <summary>
///     Pins how StartPasskeySignInAsync asks for a passkey sign-in challenge: the
///     POST /passkeys/authentication/options request it puts on the wire without a session, how the challenge maps to
///     the result with its WebAuthn options passed through untouched, and the failures the server can answer, all
///     against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StartPasskeySignInContractTests
{
    private const string ChallengePath = "/passkeys/authentication/options";
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
    public async Task StartPasskeySignInAsync_ShouldPostToTheChallengeEndpointWithTheApiKey()
    {
        this.MockChallengeResponse(200, Fixture("sign_in_challenge.json"));
        await this.client.StartPasskeySignInAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath(ChallengePath)
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task StartPasskeySignInAsync_ShouldReturnTheChallengeWithItsOptionsVerbatim_GivenNoSession()
    {
        this.MockChallengeResponse(200, Fixture("sign_in_challenge.json"));
        var challenge = await this.client.StartPasskeySignInAsync();
        new { challenge.Id, challenge.ExpiresAt, Options = challenge.Options.GetRawText() }.Should().Be(new
        {
            Id = Guid.Parse("9a6c2f4e-3b1d-4e8a-a7c5-0f2d6b9e1c38"),
            ExpiresAt = new DateTimeOffset(2026, 11, 3, 14, 50, 0, TimeSpan.Zero),
            Options = IssuedOptions(),
        },
            "the server answers {challenge_id, options, expires_at in unix seconds} to anyone, and the options are W3C JSON for the platform's WebAuthn API (supabase/auth PasskeyAuthenticationOptions)");
    }

    [TestMethod]
    public async Task StartPasskeySignInAsync_ShouldThrow_GivenSuccessWithoutAChallenge()
    {
        this.MockChallengeResponse(200, "null");
        var starting = () => this.client.StartPasskeySignInAsync();
        await starting.Should().ThrowAsync<GotrueException>().WithMessage("The auth server returned no passkey challenge.",
            "a successful answer always carries the challenge, so its absence is a server failure");
    }

    [TestMethod]
    public async Task StartPasskeySignInAsync_ShouldThrowUserTooManyRequests_GivenRateLimitReached()
    {
        this.MockChallengeResponse(429, Fixture("rate_limited_error.json"));
        var starting = () => this.client.StartPasskeySignInAsync();
        (await starting.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(UserTooManyRequests,
            "challenge issuance is rate limited per IP (supabase/auth limiterOpts.PasskeyAuthentication)");
    }

    [TestMethod]
    public async Task StartPasskeySignInAsync_ShouldThrowServerRejection_GivenMissingCaptcha()
    {
        this.MockChallengeResponse(400, Fixture("captcha_failed_error.json"));
        var starting = () => this.client.StartPasskeySignInAsync();
        (await starting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("captcha_failed",
            "the challenge endpoint sits behind the CAPTCHA check when CAPTCHA protection is on (supabase/auth verifyCaptcha)");
    }

    [TestMethod]
    public async Task StartPasskeySignInAsync_ShouldThrowServerRejection_GivenPasskeysDisabled()
    {
        this.MockChallengeResponse(404, Fixture("passkeys_disabled_error.json"));
        var starting = () => this.client.StartPasskeySignInAsync();
        (await starting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("passkey_disabled",
            "every /passkeys route answers 404 passkey_disabled when passkeys are off on the server (supabase/auth requirePasskeyEnabled)");
    }

    [TestMethod]
    public async Task StartPasskeySignInAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.MockChallengeResponse(200, Fixture("sign_in_challenge.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var starting = () => this.client.StartPasskeySignInAsync(cancellationToken: cancellation.Token);
        await starting.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void StartPasskeySignInAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.MockChallengeResponse(200, Fixture("sign_in_challenge.json"));
        BlockingCaller.Completes(() => this.client.StartPasskeySignInAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    private static string IssuedOptions()
    {
        using var issued = JsonDocument.Parse(Fixture("sign_in_challenge.json"));
        return issued.RootElement.GetProperty("options").GetRawText();
    }

    private void MockChallengeResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath(ChallengePath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SignInWithPasskey", "Data", name));
}
