#region

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Common;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.RegenerateOAuthClientSecret;

/// <summary>
///     Pins the wire contract of regenerating an OAuth client secret against a stubbed auth server: the route, the
///     service-role credentials, how the response maps onto <see cref="OAuthClientCredentials" />, the failure mapping,
///     and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RegenerateOAuthClientSecretContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid ClientId = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b");

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldPostToRegenerateRouteWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        await this.Admin.RegenerateOAuthClientSecretAsync(ClientId);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/admin/oauth/clients/6ea7e0b6-ee41-4791-802c-ea0625ee8d6b/regenerate_secret")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldReturnClientAndNewSecret()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        (await this.Admin.RegenerateOAuthClientSecretAsync(ClientId)).Should().BeEquivalentTo(new
        {
            ClientSecret = "s3cr3t-value",
            Client = new
            {
                ClientId,
                Name = "My App",
                Type = OAuthClientType.Confidential,
                TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretBasic,
            },
        }, "the auth server answers 200 with the client and its new plaintext secret, as in its OAuthClientSchema");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(ClientId);
        await regenerate.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no OAuth client*",
            "a 200 always carries the client; anything else is a broken response, not a client");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"oauth_client_not_found","msg":"OAuth client not found"}""");
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(ClientId);
        await regenerate.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
            "the auth server answers 404 oauth_client_not_found when no client has the id");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowValidationFailed_GivenPublicClient()
    {
        this.AnswerWith(HttpStatusCode.BadRequest, """{"code":400,"error_code":"validation_failed","msg":"Cannot regenerate secret for public client"}""");
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(ClientId);
        await regenerate.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.ErrorCode == "validation_failed",
            "the auth server answers 400 validation_failed for a public client, which has no secret");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowFeatureDisabled_GivenOAuthServerDisabled()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"feature_disabled","msg":"OAuth server is disabled"}""");
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(ClientId);
        await regenerate.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.ErrorCode == "feature_disabled",
            "the auth server answers 404 feature_disabled on every OAuth client route while its OAuth server is off");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(ClientId);
        await regenerate.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void RegenerateOAuthClientSecretAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        BlockingCaller.Completes(() => this.Admin.RegenerateOAuthClientSecretAsync(ClientId)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(ClientId, cancelled.Token);
        await regenerate.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/oauth/clients/*/regenerate_secret")).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
