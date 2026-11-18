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
using Supabase.Gotrue.CreateOAuthClient;
using Supabase.Gotrue.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.CreateOAuthClient;

/// <summary>
///     Pins the wire contract of registering an OAuth client against a stubbed auth server: the route, the service-role
///     credentials, how the response maps onto <see cref="OAuthClientCredentials" />, the failure mapping, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateOAuthClientContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly NewOAuthClient Client = NewOAuthClient.Confidential("My App", new[] { new Uri("https://app.example.com/callback") });

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldPostClientWithServiceRoleCredentials()
    {
        this.AnswerWith(HttpStatusCode.Created, OAuthClientResponses.Confidential);
        await this.Admin.CreateOAuthClientAsync(Client);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/admin/oauth/clients")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithJsonContentType();
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldReturnClientAndSecret_GivenConfidentialClient()
    {
        this.AnswerWith(HttpStatusCode.Created, OAuthClientResponses.Confidential);
        (await this.Admin.CreateOAuthClientAsync(Client)).Should().BeEquivalentTo(new OAuthClientCredentials
        {
            ClientSecret = "s3cr3t-value",
            Client = new OAuthClient
            {
                ClientId = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b"),
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
            },
        }, "the auth server answers 201 with the client and, for a confidential client, its secret, as in its OAuthClientSchema");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldReturnNoSecret_GivenPublicClient()
    {
        this.AnswerWith(HttpStatusCode.Created, OAuthClientResponses.Public);
        (await this.Admin.CreateOAuthClientAsync(NewOAuthClient.Public("My SPA", new[] { new Uri("http://localhost:3000/callback") }))).Should().BeEquivalentTo(new OAuthClientCredentials
        {
            ClientSecret = null,
            Client = new OAuthClient
            {
                ClientId = Guid.Parse("1f0c6b8e-3d1a-4c55-9a43-5f1a2b7c9d10"),
                Type = OAuthClientType.Public,
                TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.None,
                RegistrationType = OAuthClientRegistrationType.Manual,
                RedirectUris = new[] { new Uri("http://localhost:3000/callback") },
                GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode },
                CreatedAt = new DateTimeOffset(2026, 11, 18, 10, 20, 30, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 11, 18, 10, 20, 30, TimeSpan.Zero),
            },
        }, "the auth server generates no secret for a public client and omits an empty name and URIs");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldReturnEmptyCollections_GivenNullCollectionsInResponse()
    {
        this.AnswerWith(HttpStatusCode.Created, """{"client_id":"1f0c6b8e-3d1a-4c55-9a43-5f1a2b7c9d10","client_type":"public","token_endpoint_auth_method":"none","registration_type":"dynamic"}""");
        (await this.Admin.CreateOAuthClientAsync(Client)).Client.Should().BeEquivalentTo(new
        {
            RegistrationType = OAuthClientRegistrationType.Dynamic,
            RedirectUris = Array.Empty<Uri>(),
            GrantTypes = Array.Empty<OAuthClientGrantType>(),
        }, "collections are exposed read-only and never null, whatever the server sends");
    }

    [TestMethod]
    [DataRow("client_type", "confidential", "hybrid", "OAuth client type")]
    [DataRow("token_endpoint_auth_method", "client_secret_basic", "private_key_jwt", "OAuth client token endpoint authentication method")]
    [DataRow("registration_type", "manual", "imported", "OAuth client registration type")]
    public async Task CreateOAuthClientAsync_ShouldThrowGotrueException_GivenUnrecognizedEnumValue(string member, string current, string unknown, string description)
    {
        this.AnswerWith(HttpStatusCode.Created, OAuthClientResponses.Confidential.Replace($"\"{member}\": \"{current}\"", $"\"{member}\": \"{unknown}\""));
        var create = () => this.Admin.CreateOAuthClientAsync(Client);
        await create.Should().ThrowExactlyAsync<GotrueException>().WithMessage($"*unrecognized {description} '{unknown}'*",
            "the enums only model the values the auth server defines");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowGotrueException_GivenUnrecognizedGrantType()
    {
        this.AnswerWith(HttpStatusCode.Created, OAuthClientResponses.Confidential.Replace("\"refresh_token\"", "\"password\""));
        var create = () => this.Admin.CreateOAuthClientAsync(Client);
        await create.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*unrecognized OAuth grant type 'password'*",
            "OAuthClientGrantType only models the grants the auth server allows");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith(HttpStatusCode.Created, "null");
        var create = () => this.Admin.CreateOAuthClientAsync(Client);
        await create.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no OAuth client*",
            "a 201 always carries the created client; anything else is a broken response, not a client");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowValidationFailed_GivenRejectedSetting()
    {
        this.AnswerWith(HttpStatusCode.BadRequest, """{"code":400,"error_code":"validation_failed","msg":"redirect_uris cannot exceed 10 items"}""");
        var create = () => this.Admin.CreateOAuthClientAsync(Client);
        await create.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.ErrorCode == "validation_failed",
            "the auth server answers 400 validation_failed when a setting is invalid");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowFeatureDisabled_GivenOAuthServerDisabled()
    {
        this.AnswerWith(HttpStatusCode.NotFound, """{"code":404,"error_code":"feature_disabled","msg":"OAuth server is disabled"}""");
        var create = () => this.Admin.CreateOAuthClientAsync(Client);
        await create.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.ErrorCode == "feature_disabled",
            "the auth server answers 404 feature_disabled on every OAuth client route while its OAuth server is off");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        this.AnswerWith(HttpStatusCode.Forbidden, """{"code":403,"error_code":"not_admin","msg":"User not allowed"}""");
        var create = () => this.Admin.CreateOAuthClientAsync(Client);
        await create.Should().ThrowExactlyAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    [TestMethod]
    public void CreateOAuthClientAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(HttpStatusCode.Created, OAuthClientResponses.Confidential);
        BlockingCaller.Completes(() => this.Admin.CreateOAuthClientAsync(Client)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowArgumentNull_GivenNullClient()
    {
        var create = () => this.Admin.CreateOAuthClientAsync(null!);
        await create.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("client");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var create = () => this.Admin.CreateOAuthClientAsync(Client, cancellationToken: cancelled.Token);
        await create.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(HttpStatusCode status, string body) =>
        this.server.Given(Request.Create().WithPath("/admin/oauth/clients").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
