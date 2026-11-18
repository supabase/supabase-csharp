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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.ListOAuthClients;

/// <summary>
///     Pins the wire contract of listing OAuth clients against a stubbed auth server: the route, the service-role
///     credentials, how the response maps onto <see cref="OAuthClient" />, the failure mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListOAuthClientsContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldGetClientsWithServiceRoleCredentialsAndNoQuery()
    {
        this.AnswerWith(HttpStatusCode.OK, """{"clients":[]}""");
        await this.Admin.ListOAuthClientsAsync();
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/admin/oauth/clients")
            .WithHeader("Authorization", $"Bearer {ServiceKey}");
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldReturnClientsInServerOrder()
    {
        this.AnswerWith(HttpStatusCode.OK, $$"""{"clients":[{{OAuthClientResponses.Public}},{{OAuthClientResponses.Confidential}}]}""");
        (await this.Admin.ListOAuthClientsAsync()).Should().SatisfyRespectively(
            first => first.Should().BeEquivalentTo(new
            {
                ClientId = Guid.Parse("1f0c6b8e-3d1a-4c55-9a43-5f1a2b7c9d10"),
                Type = OAuthClientType.Public,
                TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.None,
                GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode },
            }),
            second => second.Should().BeEquivalentTo(new
            {
                ClientId = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b"),
                Name = "My App",
                Type = OAuthClientType.Confidential,
                RegistrationType = OAuthClientRegistrationType.Manual,
                RedirectUris = new[] { new Uri("https://app.example.com/callback"), new Uri("myapp://callback") },
                ClientUri = new Uri("https://app.example.com"),
                CreatedAt = new DateTimeOffset(2026, 11, 18, 10, 20, 30, 500, TimeSpan.Zero),
            }));
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldNeverExposeClientSecret()
    {
        this.AnswerWith(HttpStatusCode.OK, $$"""{"clients":[{{OAuthClientResponses.Confidential}}]}""");
        (await this.Admin.ListOAuthClientsAsync()).Should().ContainSingle().Which.ToString().Should().NotContain("s3cr3t-value",
            "OAuthClient has no secret member: the server returns the secret only on creation and regeneration");
    }

    [TestMethod]
    [DataRow("""{"clients":[]}""")]
    [DataRow("{}")]
    [DataRow("null")]
    public async Task ListOAuthClientsAsync_ShouldReturnEmptyList_GivenNoClients(string body)
    {
        this.AnswerWith(HttpStatusCode.OK, body);
        (await this.Admin.ListOAuthClientsAsync()).Should().BeEmpty("the auth server omits clients when the project has none");
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldThrowGotrueException_GivenUnrecognizedClientType()
    {
        this.AnswerWith(HttpStatusCode.OK, $$"""{"clients":[{{OAuthClientResponses.Public.Replace("\"public\"", "\"hybrid\"")}}]}""");
        var list = () => this.Admin.ListOAuthClientsAsync();
        await list.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*unrecognized OAuth client type 'hybrid'*");
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldThrowFeatureDisabled_GivenOAuthServerDisabled()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"feature_disabled","msg":"OAuth server is disabled"}""");
        var list = () => this.Admin.ListOAuthClientsAsync();
        await list.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.ErrorCode == "feature_disabled",
            "the auth server answers 404 feature_disabled on every OAuth client route while its OAuth server is off");
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var list = () => this.Admin.ListOAuthClientsAsync();
        await list.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void ListOAuthClientsAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.OK, """{"clients":[]}""");
        BlockingCaller.Completes(() => this.Admin.ListOAuthClientsAsync()).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var list = () => this.Admin.ListOAuthClientsAsync(cancelled.Token);
        await list.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath("/admin/oauth/clients").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
