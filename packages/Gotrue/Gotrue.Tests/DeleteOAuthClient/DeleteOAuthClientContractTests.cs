#region

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.DeleteOAuthClient;

/// <summary>
///     Pins the wire contract of deleting an OAuth client against a stubbed auth server: the route, the service-role
///     credentials, the failure mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteOAuthClientContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid ClientId = Guid.Parse("0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76");

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldDeleteClientByIdWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.NoContent, string.Empty);
        await this.Admin.DeleteOAuthClientAsync(ClientId);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithPath("/admin/oauth/clients/0b3c6a4e-5f21-4d8e-9a7b-2c1d0e9f8a76")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldComplete_GivenNoContentResponse()
    {
        this.AnswerWith(HttpStatusCode.NoContent, string.Empty);
        var delete = () => this.Admin.DeleteOAuthClientAsync(ClientId);
        await delete.Should().NotThrowAsync("the auth server answers 204 with no body when it deletes the client");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"oauth_client_not_found","msg":"OAuth client not found"}""");
        var delete = () => this.Admin.DeleteOAuthClientAsync(ClientId);
        await delete.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
                "the auth server answers 404 oauth_client_not_found when no client has the id, or it was already deleted");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldThrowFeatureDisabled_GivenOAuthServerDisabled()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"feature_disabled","msg":"OAuth server is disabled"}""");
        var delete = () => this.Admin.DeleteOAuthClientAsync(ClientId);
        await delete.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "feature_disabled",
                "the auth server answers 404 feature_disabled on every OAuth client route while its OAuth server is off");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var delete = () => this.Admin.DeleteOAuthClientAsync(ClientId);
        await delete.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void DeleteOAuthClientAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.NoContent, string.Empty);
        BlockingCaller.Completes(() => this.Admin.DeleteOAuthClientAsync(ClientId)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var delete = () => this.Admin.DeleteOAuthClientAsync(ClientId, cancelled.Token);
        await delete.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/oauth/clients/*")).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
