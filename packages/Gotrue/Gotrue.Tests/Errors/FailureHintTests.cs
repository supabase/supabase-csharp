#region

using System.Threading.Tasks;
using FluentAssertions;
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
    [DataRow(400, "Invalid login credentials", UserBadLogin, DisplayName = "400 invalid login")]
    [DataRow(400, "Email not confirmed", UserEmailNotConfirmed, DisplayName = "400 email not confirmed")]
    [DataRow(400, "Invalid Refresh Token", InvalidRefreshToken, DisplayName = "400 invalid refresh token")]
    [DataRow(400, "refresh_token_not_found", InvalidRefreshToken, DisplayName = "400 refresh token not found")]
    [DataRow(400, "Refresh token is not valid", InvalidRefreshToken, DisplayName = "400 malformed refresh token")]
    [DataRow(400, "Phone number is invalid", UserBadPhoneNumber, DisplayName = "400 bad phone")]
    [DataRow(400, "Email address is invalid", UserBadEmailAddress, DisplayName = "400 bad email")]
    [DataRow(400, "You must provide a value", UserMissingInformation, DisplayName = "400 missing information")]
    [DataRow(401, "This endpoint requires a Bearer token", AdminTokenRequired, DisplayName = "401 bearer required")]
    [DataRow(403, "Invalid token", AdminTokenRequired, DisplayName = "403 invalid token")]
    [DataRow(403, "invalid JWT", AdminTokenRequired, DisplayName = "403 invalid JWT")]
    [DataRow(404, "No SSO provider assigned for this domain", SsoDomainNotFound, DisplayName = "404 sso domain not found")]
    [DataRow(404, "No such SSO provider", SsoProviderNotFound, DisplayName = "404 sso provider not found")]
    [DataRow(422, "User already registered", UserAlreadyRegistered, DisplayName = "422 already registered")]
    [DataRow(422, "Phone and Email are both invalid", UserBadMultiple, DisplayName = "422 bad phone and email")]
    [DataRow(422, "Invalid email and password", UserBadMultiple, DisplayName = "422 bad email and password")]
    [DataRow(422, "Password is too weak", UserBadPassword, DisplayName = "422 bad password")]
    [DataRow(429, "Too many requests", UserTooManyRequests, DisplayName = "429 rate limited")]
    [DataRow(500, "boom", Unknown, DisplayName = "unrecognized status")]
    [DataRow(502, "boom", NetworkError, DisplayName = "standard server/gateway errors")]
    [DataRow(503, "boom", NetworkError, DisplayName = "standard server/gateway errors")]
    [DataRow(504, "boom", NetworkError, DisplayName = "standard server/gateway errors")]
    [DataRow(520, "boom", CloudflareNetworkError, DisplayName = "cloudflare errors")]
    [DataRow(521, "boom", CloudflareNetworkError, DisplayName = "cloudflare errors")]
    [DataRow(522, "boom", CloudflareNetworkError, DisplayName = "cloudflare errors")]
    [DataRow(523, "boom", CloudflareNetworkError, DisplayName = "cloudflare errors")]
    [DataRow(524, "boom", CloudflareNetworkError, DisplayName = "cloudflare errors")]
    [DataRow(530, "boom", CloudflareNetworkError, DisplayName = "cloudflare errors")]
    public async Task DetectReason_ShouldMapServerErrorToReason(
        int statusCode,
        string body,
        FailureHint.Reason expected
    )
    {
        this.StubSignUp(statusCode, body);
        var signUp = () => TestClients.Against(this.server).SignUp(RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        exception
            .Which.Reason.Should()
            .Be(expected, $"status {statusCode} with body \"{body}\" classifies as {expected}");
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
