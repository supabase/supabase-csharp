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
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.GetRecoveryCodesStatus;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.GetRecoveryCodesStatus;

/// <summary>
///     Pins how GetRecoveryCodesStatusAsync reads the signed-in user's MFA recovery codes status: the
///     GET /factors/recovery-codes request it puts on the wire, how the status body maps to the result, the
///     not-enrolled answer that maps to null, and the failures raised before and after the request, all against
///     a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RecoveryCodesStatusContractTests
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
    public async Task GetRecoveryCodesStatusAsync_ShouldGetTheStatusWithTheSessionBearer()
    {
        this.SignIn();
        this.MockStatusResponse(200, Fixture("recovery_codes_status.json"));
        await this.client.GetRecoveryCodesStatusAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/factors/recovery-codes")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldReturnTheFactorAndCodeCounts_GivenEnrolledUser()
    {
        this.SignIn();
        this.MockStatusResponse(200, Fixture("recovery_codes_status.json"));
        var status = await this.client.GetRecoveryCodesStatusAsync();
        status.Should().Be(new RecoveryCodesStatus { FactorId = "2b306a77-21dc-4110-ba71-537cb56b9e98", Total = 10, Remaining = 7 },
            "the status is the id, total and remaining of MFARecoveryCodesStatusSchema (auth OpenAPI, GET /factors/recovery-codes)");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldReturnNull_GivenUserWithoutRecoveryCodes()
    {
        this.SignIn();
        this.MockStatusResponse(404, Fixture("not_enrolled_error.json"));
        var status = await this.client.GetRecoveryCodesStatusAsync();
        status.Should().BeNull("GoTrue answers 404 mfa_factor_not_found when the user has not enrolled recovery codes, which is a status, not a failure");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldThrowServerRejection_GivenNotFoundForAnotherReason()
    {
        this.SignIn();
        this.MockStatusResponse(404, Fixture("user_not_found_error.json"));
        var reading = () => this.client.GetRecoveryCodesStatusAsync();
        (await reading.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("user_not_found",
            "only mfa_factor_not_found means the user has no recovery codes; any other 404 is a failure");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldThrow_GivenSuccessWithoutBody()
    {
        this.SignIn();
        this.MockStatusResponse(200, "null");
        var reading = () => this.client.GetRecoveryCodesStatusAsync();
        await reading.Should().ThrowAsync<GotrueException>("a 200 answer must carry the status; an empty one is a broken server, not a user without codes")
            .WithMessage("The auth server returned no recovery codes status.");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldThrowServerRejection_GivenExpiredJwt()
    {
        this.SignIn();
        this.MockStatusResponse(403, Fixture("expired_jwt_error.json"));
        var reading = () => this.client.GetRecoveryCodesStatusAsync();
        (await reading.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("bad_jwt",
            "GoTrue rejects an expired access token with error_code bad_jwt");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var reading = () => this.client.GetRecoveryCodesStatusAsync();
        (await reading.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "GET /factors/recovery-codes requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var reading = () => this.client.GetRecoveryCodesStatusAsync();
        (await reading.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "GET /factors/recovery-codes requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var reading = () => this.client.GetRecoveryCodesStatusAsync();
        await reading.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldThrowOffline_GivenClientIsOffline()
    {
        this.SignIn();
        this.client.Online = false;
        var reading = () => this.client.GetRecoveryCodesStatusAsync();
        (await reading.Should().ThrowAsync<GotrueException>().WithMessage("Only supported when online")).Which.Reason.Should().Be(Offline,
            "the status is read from the server, like the other MFA operations");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockStatusResponse(200, Fixture("recovery_codes_status.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reading = () => this.client.GetRecoveryCodesStatusAsync(cancellation.Token);
        await reading.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void GetRecoveryCodesStatusAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockStatusResponse(200, Fixture("recovery_codes_status.json"));
        BlockingCaller.Completes(() => this.client.GetRecoveryCodesStatusAsync()).Should().BeTrue(
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

    private void MockStatusResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/factors/recovery-codes").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GetRecoveryCodesStatus", "Data", name));
}
