#region

using System;
using System.Collections.Generic;
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
using Supabase.Gotrue.UpdateOAuthClient;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.UpdateOAuthClient;

/// <summary>
///     Pins the wire contract of updating an OAuth client against a stubbed auth server: the route, the service-role
///     credentials, how the response maps onto <see cref="OAuthClient" />, the failure mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateOAuthClientContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly Guid ClientId = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b");

    private static readonly OAuthClientChanges Changes = new() { Name = "My App" };

    public static IEnumerable<object[]> SingleOptionChanges =>
        new[]
        {
            new object[] { new OAuthClientOptions { GrantTypes = new[] { OAuthClientGrantType.RefreshToken } } },
            new object[] { new OAuthClientOptions { ClientUri = new Uri("https://app.example.com") } },
            new object[] { new OAuthClientOptions { LogoUri = new Uri("https://app.example.com/logo.png") } },
        };

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldPutChangesWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        await this.Admin.UpdateOAuthClientAsync(ClientId, Changes);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Put)
            .WithPath("/admin/oauth/clients/6ea7e0b6-ee41-4791-802c-ea0625ee8d6b")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithJsonContentType();
    }

    [TestMethod]
    [DynamicData(nameof(SingleOptionChanges))]
    public async Task UpdateOAuthClientAsync_ShouldSendRequest_GivenOnlyOneOptionSet(OAuthClientOptions options)
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        await this.Admin.UpdateOAuthClientAsync(ClientId, new OAuthClientChanges { Options = options });
        this.server.CountReceivedRequests().Should().Be(1, "any single option is a change the server can apply");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldReturnUpdatedClient()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        (await this.Admin.UpdateOAuthClientAsync(ClientId, Changes)).Should().BeEquivalentTo(new OAuthClient
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
        }, "the auth server answers 200 with the updated client, as in its OAuthClientSchema, and never repeats the secret");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.OK, "null");
        var update = () => this.Admin.UpdateOAuthClientAsync(ClientId, Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no OAuth client*",
            "a 200 always carries the updated client; anything else is a broken response, not a client");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"oauth_client_not_found","msg":"OAuth client not found"}""");
        var update = () => this.Admin.UpdateOAuthClientAsync(ClientId, Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
            "the auth server answers 404 oauth_client_not_found when no client has the id");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowValidationFailed_GivenRejectedSetting()
    {
        this.AnswerWith(HttpStatusCode.BadRequest, """{"code":400,"error_code":"validation_failed","msg":"token_endpoint_auth_method 'none' is not valid for client_type 'confidential'"}""");
        var update = () => this.Admin.UpdateOAuthClientAsync(ClientId, Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.ErrorCode == "validation_failed",
            "the auth server answers 400 validation_failed when a setting is invalid for the client");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowFeatureDisabled_GivenOAuthServerDisabled()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"feature_disabled","msg":"OAuth server is disabled"}""");
        var update = () => this.Admin.UpdateOAuthClientAsync(ClientId, Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.ErrorCode == "feature_disabled",
            "the auth server answers 404 feature_disabled on every OAuth client route while its OAuth server is off");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var update = () => this.Admin.UpdateOAuthClientAsync(ClientId, Changes);
        await update.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void UpdateOAuthClientAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, OAuthClientResponses.Confidential);
        BlockingCaller.Completes(() => this.Admin.UpdateOAuthClientAsync(ClientId, Changes)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var update = () => this.Admin.UpdateOAuthClientAsync(ClientId, Changes, cancelled.Token);
        await update.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("/admin/oauth/clients/*")).UsingPut())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
