#region

using System;
using System.IO;
using System.Net.Http;
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

namespace Gotrue.Tests.RegenerateRecoveryCodes;

/// <summary>
///     Pins how RegenerateRecoveryCodesAsync replaces the signed-in user's MFA recovery codes: the
///     POST /factors/recovery-codes/regenerate request it puts on the wire, how the new set maps to the result, and the
///     failures raised before and after the request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RegenerateRecoveryCodesContractTests
{
    private const string AccessToken = "an-access-token";
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
    public async Task RegenerateRecoveryCodesAsync_ShouldPostTheRegenerationWithTheSessionBearer()
    {
        this.SignIn();
        this.MockRegenerateResponse(200, Fixture("regenerated_codes.json"));
        await this.client.RegenerateRecoveryCodesAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/factors/recovery-codes/regenerate")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldReturnTheFactorAndItsNewCodes_GivenRegeneratedSet()
    {
        this.SignIn();
        this.MockRegenerateResponse(200, Fixture("regenerated_codes.json"));
        var recoveryCodes = await this.client.RegenerateRecoveryCodesAsync();
        recoveryCodes.Should().BeEquivalentTo(new RecoveryCodes
        {
            FactorId = "2b306a77-21dc-4110-ba71-537cb56b9e98",
            FriendlyName = "Backup codes",
            Codes = new[] { "7tq3m8wkx2pn9hra", "c5vz4e8ry6md2qwn", "p9x2k7hn3tb6wz4m" },
        },
            options => options.WithStrictOrdering(),
            "the result is the id, friendly_name and codes of MFARecoveryCodesSchema (auth OpenAPI, POST /factors/recovery-codes/regenerate)");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrow_GivenSuccessWithoutBody()
    {
        this.SignIn();
        this.MockRegenerateResponse(200, "null");
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync();
        await regenerating.Should().ThrowAsync<GotrueException>("a 200 answer must carry the new codes, which cannot be read back later")
            .WithMessage("The auth server returned no recovery codes.");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowServerRejection_GivenSessionBelowAal2()
    {
        this.SignIn();
        this.MockRegenerateResponse(403, Fixture("insufficient_aal_error.json"));
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "POST /factors/recovery-codes/regenerate requires an aal2 session (auth OpenAPI, 403 insufficient_aal)");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowServerRejection_GivenUserWithoutRecoveryCodes()
    {
        this.SignIn();
        this.MockRegenerateResponse(404, Fixture("not_enrolled_error.json"));
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_factor_not_found",
            "only an enrolled set can be replaced (auth OpenAPI, 404 mfa_factor_not_found)");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowServerRejection_GivenServerWithRecoveryCodesDisabled()
    {
        this.SignIn();
        this.MockRegenerateResponse(422, Fixture("enroll_not_enabled_error.json"));
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_recovery_codes_enroll_not_enabled",
            "regeneration is refused while recovery codes enrollment is disabled (auth OpenAPI, 422 mfa_recovery_codes_enroll_not_enabled)");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "POST /factors/recovery-codes/regenerate requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "POST /factors/recovery-codes/regenerate requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync();
        await regenerating.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockRegenerateResponse(200, Fixture("regenerated_codes.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var regenerating = () => this.client.RegenerateRecoveryCodesAsync(cancellation.Token);
        await regenerating.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void RegenerateRecoveryCodesAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockRegenerateResponse(200, Fixture("regenerated_codes.json"));
        BlockingCaller.Completes(() => this.client.RegenerateRecoveryCodesAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    private void SignIn() => this.SignIn(new Session { AccessToken = AccessToken, RefreshToken = "a-refresh-token", ExpiresIn = 3600, User = new User { Id = "user-id-123" } });

    private void SignIn(Session session)
    {
        var persistence = SessionPersistenceSubstitute.Tracking();
        persistence.SaveSession(session);
        this.client.SetPersistence(persistence);
        this.client.LoadSession();
    }

    private void MockRegenerateResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/factors/recovery-codes/regenerate").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RegenerateRecoveryCodes", "Data", name));
}
