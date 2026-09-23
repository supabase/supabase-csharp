#region

using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.Errors;

/// <summary>
///     Pins how the SDK maps a GoTrue error response (HTTP status + body) to a <see cref="FailureHint.Reason" />.
///     Driven through the real transport against a stubbed server so the classification is exercised on the
///     same path a caller hits, without depending on the live stack producing each specific error.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class FailureHintTests
{
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    [DataRow(429, UserTooManyRequests, DisplayName = "429 rate limited")]
    [DataRow(500, Unknown, DisplayName = "unrecognized status")]
    [DataRow(502, NetworkError, DisplayName = "502 gateway error")]
    [DataRow(503, NetworkError, DisplayName = "503 gateway error")]
    [DataRow(504, NetworkError, DisplayName = "504 gateway error")]
    [DataRow(520, CloudflareNetworkError, DisplayName = "520 cloudflare error")]
    [DataRow(521, CloudflareNetworkError, DisplayName = "521 cloudflare error")]
    [DataRow(522, CloudflareNetworkError, DisplayName = "522 cloudflare error")]
    [DataRow(523, CloudflareNetworkError, DisplayName = "523 cloudflare error")]
    [DataRow(524, CloudflareNetworkError, DisplayName = "524 cloudflare error")]
    [DataRow(530, CloudflareNetworkError, DisplayName = "530 cloudflare error")]
    public async Task DetectReason_ShouldFallBackToStatusCode_GivenNoErrorCode(int statusCode, FailureHint.Reason expected)
    {
        this.StubSignUp(statusCode, "an upstream gateway page");
        var signUp = () => TestClients.Against(this.server).SignUp(RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        exception
            .Which.Reason.Should()
            .Be(expected, $"a bodyless status {statusCode} classifies as {expected}");
    }

    [TestMethod]
    [DataRow("invalid_credentials", UserBadLogin, DisplayName = "invalid_credentials")]
    [DataRow("email_not_confirmed", UserEmailNotConfirmed, DisplayName = "email_not_confirmed")]
    [DataRow("email_address_invalid", UserBadEmailAddress, DisplayName = "email_address_invalid")]
    [DataRow("refresh_token_not_found", InvalidRefreshToken, DisplayName = "refresh_token_not_found")]
    [DataRow("refresh_token_already_used", InvalidRefreshToken, DisplayName = "refresh_token_already_used")]
    [DataRow("user_already_exists", UserAlreadyRegistered, DisplayName = "user_already_exists")]
    [DataRow("email_exists", UserAlreadyRegistered, DisplayName = "email_exists")]
    [DataRow("phone_exists", UserAlreadyRegistered, DisplayName = "phone_exists")]
    [DataRow("weak_password", UserBadPassword, DisplayName = "weak_password")]
    [DataRow("over_request_rate_limit", UserTooManyRequests, DisplayName = "over_request_rate_limit")]
    [DataRow("over_email_send_rate_limit", UserTooManyRequests, DisplayName = "over_email_send_rate_limit")]
    [DataRow("over_sms_send_rate_limit", UserTooManyRequests, DisplayName = "over_sms_send_rate_limit")]
    [DataRow("bad_jwt", AdminTokenRequired, DisplayName = "bad_jwt")]
    [DataRow("no_authorization", AdminTokenRequired, DisplayName = "no_authorization")]
    [DataRow("not_admin", AdminTokenRequired, DisplayName = "not_admin")]
    [DataRow("sso_provider_not_found", SsoProviderNotFound, DisplayName = "sso_provider_not_found")]
    [DataRow("mfa_verification_failed", MfaChallengeUnverified, DisplayName = "mfa_verification_failed")]
    [DataRow("mfa_verification_rejected", MfaChallengeUnverified, DisplayName = "mfa_verification_rejected")]
    [DataRow("mfa_challenge_expired", MfaChallengeUnverified, DisplayName = "mfa_challenge_expired")]
    public async Task DetectReason_ShouldMapErrorCodeToReason(string errorCode, FailureHint.Reason expected)
    {
        this.StubSignUp(400, $$"""{"code":400,"error_code":"{{errorCode}}","msg":"a server message"}""");
        var signUp = () => TestClients.Against(this.server).SignUp(RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should().Be(expected, $"error_code \"{errorCode}\" classifies as {expected}");
        exception.Which.ErrorCode.Should().Be(errorCode, "the raw server error_code is exposed for precise handling");
    }

    [TestMethod]
    public async Task DetectReason_ShouldClassifyFromErrorCode_GivenConflictingMessageText()
    {
        this.StubSignUp(400, """{"code":400,"error_code":"invalid_credentials","msg":"Email not confirmed"}""");
        var signUp = () => TestClients.Against(this.server).SignUp(RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should()
            .Be(UserBadLogin, "classification comes from the machine-readable error_code, not the message text");
    }

    [TestMethod]
    public async Task DetectReason_ShouldBeUnknown_GivenAnUnmappedErrorCode()
    {
        this.StubSignUp(400, """{"code":400,"error_code":"validation_failed","msg":"You must provide a value"}""");
        var signUp = () => TestClients.Against(this.server).SignUp(RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        using (new AssertionScope())
        {
            exception.Which.Reason.Should().Be(Unknown, "a generic/unmapped code resolves to Unknown rather than guessing from message text");
            exception.Which.ErrorCode.Should().Be("validation_failed", "the raw code is still surfaced so callers can branch on it");
        }
    }

    [TestMethod]
    public async Task ErrorCode_ShouldBeNull_GivenANonJsonBody()
    {
        this.StubSignUp(502, "an upstream gateway page");
        var signUp = () => TestClients.Against(this.server).SignUp(RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        using (new AssertionScope())
        {
            exception.Which.ErrorCode.Should().BeNull("a gateway/Cloudflare page carries no error_code");
            exception.Which.Reason.Should().Be(NetworkError, "status-code classification still applies");
        }
    }

    private void StubSignUp(int statusCode, string body) =>
        this
            .server.Given(Request.Create().WithPath("/signup").UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(statusCode)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(body)
            );
}
