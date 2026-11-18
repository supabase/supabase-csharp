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

namespace Gotrue.Tests.GetOAuthClient;

/// <summary>
///     Pins the wire contract of fetching an OAuth client against a stubbed auth server: the route, the service-role
///     credentials, how the response maps onto <see cref="OAuthClient" />, the failure mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetOAuthClientContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid ClientId = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b");

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldGetClientByIdWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        await this.Admin.GetOAuthClientAsync(ClientId);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/admin/oauth/clients/6ea7e0b6-ee41-4791-802c-ea0625ee8d6b")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldReturnClientWithoutSecret_GivenConfidentialClient()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        (await this.Admin.GetOAuthClientAsync(ClientId)).Should().BeEquivalentTo(new OAuthClient
        {
            ClientId = ClientId,
            Name = "My App",
            Type = OAuthClientType.Confidential,
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretBasic,
            RegistrationType = OAuthClientRegistrationType.Manual,
            RedirectUris = new[] { new Uri("https://app.example.com/callback"), new Uri("myapp://callback") },
            GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode, OAuthClientGrantType.RefreshToken },
            ClientUri = new Uri("https://app.example.com"),
            LogoUri = new Uri("https://app.example.com/logo.png"),
            CreatedAt = new DateTimeOffset(2026, 11, 18, 10, 20, 30, 500, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 11, 18, 10, 20, 31, TimeSpan.Zero),
        }, "the auth server answers 200 with the client, as in its OAuthClientSchema, and the client has no secret to expose");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldReturnClient_GivenPublicClient()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Public);
        (await this.Admin.GetOAuthClientAsync(ClientId)).Should().BeEquivalentTo(new OAuthClient
        {
            ClientId = Guid.Parse("1f0c6b8e-3d1a-4c55-9a43-5f1a2b7c9d10"),
            Type = OAuthClientType.Public,
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.None,
            RegistrationType = OAuthClientRegistrationType.Manual,
            RedirectUris = new[] { new Uri("http://localhost:3000/callback") },
            GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode },
            CreatedAt = new DateTimeOffset(2026, 11, 18, 10, 20, 30, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 11, 18, 10, 20, 30, TimeSpan.Zero),
        }, "the auth server omits an empty name and URIs");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var get = () => this.Admin.GetOAuthClientAsync(ClientId);
        await get.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no OAuth client*",
            "a 200 always carries the client; anything else is a broken response, not a client");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"oauth_client_not_found","msg":"OAuth client not found"}""");
        var get = () => this.Admin.GetOAuthClientAsync(ClientId);
        await get.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
                "the auth server answers 404 oauth_client_not_found when no client has the id, or it was deleted");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldThrowFeatureDisabled_GivenOAuthServerDisabled()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"feature_disabled","msg":"OAuth server is disabled"}""");
        var get = () => this.Admin.GetOAuthClientAsync(ClientId);
        await get.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "feature_disabled",
                "the auth server answers 404 feature_disabled on every OAuth client route while its OAuth server is off");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var get = () => this.Admin.GetOAuthClientAsync(ClientId);
        await get.Should().ThrowExactlyAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void GetOAuthClientAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        BlockingCaller.Completes(() => this.Admin.GetOAuthClientAsync(ClientId)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var get = () => this.Admin.GetOAuthClientAsync(ClientId, cancelled.Token);
        await get.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/oauth/clients/*")).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
