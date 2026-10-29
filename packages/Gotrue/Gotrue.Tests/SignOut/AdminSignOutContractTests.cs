#region

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.SignOut;

/// <summary>
///     Pins the wire contract of signing a user out as an admin against a stubbed auth server: the scope, the user's
///     token as the credentials instead of the service key, failures, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class AdminSignOutContractTests
{
    private const string ServiceKey = "service-role-jwt";
    private const string UserAccessToken = "user-access-token";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignOutAsync_ShouldPostToLogoutWithUserAccessTokenAndConfiguredApiKey()
    {
        this.AnswerLogoutWith(204);
        await this.Admin.SignOutAsync(UserAccessToken);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/logout")
            .WithHeader("Authorization", $"Bearer {UserAccessToken}")
            .WithHeader("apikey", MockGotrueServer.ApiKey);
    }

    [TestMethod]
    public async Task SignOutAsync_ShouldSendGlobalScope_GivenNoScope()
    {
        this.AnswerLogoutWith(204);
        await this.Admin.SignOutAsync(UserAccessToken);
        this.server.VerifySingleReceivedRequest().WithQueryParam("scope", "global");
    }

    [TestMethod]
    [DataRow(SignOutScope.Global, "global")]
    [DataRow(SignOutScope.Local, "local")]
    [DataRow(SignOutScope.Others, "others")]
    public async Task SignOutAsync_ShouldSendTheRequestedScope(SignOutScope scope, string expected)
    {
        this.AnswerLogoutWith(204);
        await this.Admin.SignOutAsync(UserAccessToken, scope);
        this.server.VerifySingleReceivedRequest().WithQueryParam("scope", expected);
    }

    [TestMethod]
    public async Task SignOutAsync_ShouldThrowSessionNotFound_GivenSessionAlreadyRevoked()
    {
        this.AnswerLogoutWith(403, """{"code":403,"error_code":"session_not_found","msg":"Session from session_id claim in JWT does not exist"}""");
        var signOut = () => this.Admin.SignOutAsync(UserAccessToken);
        await signOut.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "session_not_found",
                "unlike a client's own sign-out, an admin sign-out reports a session that is already gone");
    }

    [TestMethod]
    public async Task SignOutAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var signOut = () => this.Admin.SignOutAsync(UserAccessToken, cancellationToken: cancelled.Token);
        await signOut.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin =>
        new(ServiceKey, new ClientOptions { Url = this.server.Url, Headers = { ["apikey"] = MockGotrueServer.ApiKey } });

    private void AnswerLogoutWith(int status, string body = "") =>
        this.server.Given(Request.Create().WithPath("/logout").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
