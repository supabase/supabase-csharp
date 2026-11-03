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
using Supabase.Gotrue.GenerateRecoveryCodes;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.GenerateRecoveryCodes;

/// <summary>
///     Pins how GenerateRecoveryCodesAsync generates the signed-in user's MFA recovery codes: the
///     POST /factors/recovery-codes request it puts on the wire, how the generated set maps to the result, and the
///     failures raised before and after the request, all against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GenerateRecoveryCodesContractTests
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
    public async Task GenerateRecoveryCodesAsync_ShouldPostTheGenerationWithTheSessionBearer()
    {
        this.SignIn();
        this.MockGenerateResponse(200, Fixture("recovery_codes.json"));
        await this.client.GenerateRecoveryCodesAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/factors/recovery-codes")
            .WithHeader("Authorization", $"Bearer {AccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldReturnTheFactorAndItsCodes_GivenGeneratedSet()
    {
        this.SignIn();
        this.MockGenerateResponse(200, Fixture("recovery_codes.json"));
        var recoveryCodes = await this.client.GenerateRecoveryCodesAsync(new GenerateRecoveryCodesOptions { FriendlyName = "Backup codes" });
        recoveryCodes.Should().BeEquivalentTo(new RecoveryCodes
        {
            FactorId = "2b306a77-21dc-4110-ba71-537cb56b9e98",
            FriendlyName = "Backup codes",
            Codes = new[] { "k4m9x7qp2ab8ht3z", "9wze6r5npd4cmq7v", "2h8kqw3m7xt49rpn" },
        },
            options => options.WithStrictOrdering(),
            "the result is the id, friendly_name and codes of MFARecoveryCodesSchema (auth OpenAPI, POST /factors/recovery-codes)");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldReturnNoCodes_GivenSetWithoutCodes()
    {
        this.SignIn();
        this.MockGenerateResponse(200, Fixture("recovery_codes_without_codes.json"));
        var recoveryCodes = await this.client.GenerateRecoveryCodesAsync();
        recoveryCodes.Codes.Should().BeEmpty("the server omits an empty codes array, and the codes are never null");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrow_GivenSuccessWithoutBody()
    {
        this.SignIn();
        this.MockGenerateResponse(200, "null");
        var generating = () => this.client.GenerateRecoveryCodesAsync();
        await generating.Should().ThrowAsync<GotrueException>("a 200 answer must carry the generated codes, which cannot be read back later")
            .WithMessage("The auth server returned no recovery codes.");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrowServerRejection_GivenSessionBelowAal2()
    {
        this.SignIn();
        this.MockGenerateResponse(403, Fixture("insufficient_aal_error.json"));
        var generating = () => this.client.GenerateRecoveryCodesAsync();
        (await generating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "POST /factors/recovery-codes requires an aal2 session (auth OpenAPI, 403 insufficient_aal)");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrowServerRejection_GivenUserWithoutOtherVerifiedFactor()
    {
        this.SignIn();
        this.MockGenerateResponse(422, Fixture("sole_factor_error.json"));
        var generating = () => this.client.GenerateRecoveryCodesAsync();
        (await generating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_recovery_codes_sole_factor",
            "recovery codes can never be the user's only second factor (auth OpenAPI, 422 mfa_recovery_codes_sole_factor)");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrowServerRejection_GivenUserWithRecoveryCodes()
    {
        this.SignIn();
        this.MockGenerateResponse(422, Fixture("already_enrolled_error.json"));
        var generating = () => this.client.GenerateRecoveryCodesAsync();
        (await generating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_verified_factor_exists",
            "a user has at most one set of recovery codes; regenerate replaces it (auth OpenAPI, 422 mfa_verified_factor_exists)");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrowNoSessionFound_GivenNoSession()
    {
        var generating = () => this.client.GenerateRecoveryCodesAsync();
        (await generating.Should().ThrowAsync<GotrueException>().WithMessage("A valid session is required.")).Which.Reason.Should().Be(NoSessionFound,
            "POST /factors/recovery-codes requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrowNoSessionFound_GivenSessionWithoutAccessToken()
    {
        this.SignIn(new Session { RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var generating = () => this.client.GenerateRecoveryCodesAsync();
        (await generating.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(NoSessionFound,
            "POST /factors/recovery-codes requires the signed-in user's access token");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldNotSendARequest_GivenNoSession()
    {
        var generating = () => this.client.GenerateRecoveryCodesAsync();
        await generating.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, "the missing session is detected before the wire");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldCancelTheRequest_GivenCancelledToken()
    {
        this.SignIn();
        this.MockGenerateResponse(200, Fixture("recovery_codes.json"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var generating = () => this.client.GenerateRecoveryCodesAsync(cancellationToken: cancellation.Token);
        await generating.Should().ThrowAsync<OperationCanceledException>("the cancellation token reaches the HTTP call");
    }

    [TestMethod]
    public void GenerateRecoveryCodesAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.SignIn();
        this.MockGenerateResponse(200, Fixture("recovery_codes.json"));
        BlockingCaller.Completes(() => this.client.GenerateRecoveryCodesAsync()).Should().BeTrue(
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

    private void MockGenerateResponse(int statusCode, string body) =>
        this.server
            .Given(Request.Create().WithPath("/factors/recovery-codes").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GenerateRecoveryCodes", "Data", name));
}
