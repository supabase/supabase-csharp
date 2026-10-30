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
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.UnenrollRecoveryCodes;

/// <summary>
///     Pins how UnenrollRecoveryCodesAsync removes the signed-in user's MFA recovery codes: the
///     DELETE /factors/recovery-codes request it puts on the wire, and the failures raised before and after the
///     request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UnenrollRecoveryCodesContractTests
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
    public async Task UnenrollRecoveryCodesAsync_ShouldDeleteTheRecoveryCodesWithTheSessionBearer()
    {
        this.SignIn();
        this.MockUnenrollResponse(200, Fixture("unenrolled.json"));
        await this.client.UnenrollRecoveryCodesAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithPath("/factors/recovery-codes")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldComplete_GivenEnrolledUser()
    {
        this.SignIn();
        this.MockUnenrollResponse(200, Fixture("unenrolled.json"));
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync();
        await unenrolling.Should().NotThrowAsync("GoTrue answers 200 with the removed factor id once the recovery codes are deleted");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldThrowServerRejection_GivenSessionBelowAal2()
    {
        this.SignIn();
        this.MockUnenrollResponse(403, Fixture("insufficient_aal_error.json"));
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync();
        (await unenrolling.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "DELETE /factors/recovery-codes requires an aal2 session (auth OpenAPI, 403 insufficient_aal)");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldThrowServerRejection_GivenUserWithoutRecoveryCodes()
    {
        this.SignIn();
        this.MockUnenrollResponse(404, Fixture("not_enrolled_error.json"));
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync();
        (await unenrolling.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_factor_not_found",
            "there is nothing to remove when the user has not enrolled recovery codes (auth OpenAPI, 404 mfa_factor_not_found)");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync();
        (await unenrolling.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "DELETE /factors/recovery-codes requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync();
        (await unenrolling.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "DELETE /factors/recovery-codes requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync();
        await unenrolling.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldThrowOffline_GivenClientIsOffline()
    {
        this.SignIn();
        this.client.Online = false;
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync();
        (await unenrolling.Should().ThrowAsync<GotrueException>().WithMessage("Only supported when online")).Which.Reason.Should().Be(Offline,
            "removing the recovery codes needs the server, like the other MFA operations");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockUnenrollResponse(200, Fixture("unenrolled.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var unenrolling = () => this.client.UnenrollRecoveryCodesAsync(cancellation.Token);
        await unenrolling.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void UnenrollRecoveryCodesAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockUnenrollResponse(200, Fixture("unenrolled.json"));
        BlockingCaller.Completes(() => this.client.UnenrollRecoveryCodesAsync()).Should().BeTrue(
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

    private void MockUnenrollResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/factors/recovery-codes").UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnenrollRecoveryCodes", "Data", name));
}
