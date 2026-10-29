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
using Supabase.Gotrue.CreateCustomProvider;
using Supabase.Gotrue.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.CreateCustomProvider;

/// <summary>
///     Pins the wire contract of creating a custom OAuth/OIDC provider against a stubbed auth server: the route,
///     the service-role credentials, how the response maps onto <see cref="CustomProvider" />, and cancellation.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateCustomProviderContractTests
{
    private const string ServiceKey = "service-role-jwt";

    private static readonly NewCustomProvider OAuth2Provider = NewCustomProvider.OAuth2("custom:customauth", "Custom Auth Provider",
        "client-id", "client-secret", new Uri("https://provider.example.com/oauth/authorize"),
        new Uri("https://provider.example.com/oauth/token"), new Uri("https://provider.example.com/oauth/userinfo"));

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldPostProviderWithServiceRoleCredentials()
    {
        this.AnswerWith(CustomProviderResponses.OAuth2);
        await this.Admin.CreateCustomProviderAsync(OAuth2Provider);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Post)
            .WithPath("/admin/custom-providers")
            .WithHeader("Authorization", $"Bearer {ServiceKey}")
            .WithJsonContentType();
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldReturnRegisteredProvider_GivenOAuth2Provider()
    {
        this.AnswerWith(CustomProviderResponses.OAuth2);
        (await this.Admin.CreateCustomProviderAsync(OAuth2Provider)).Should().BeEquivalentTo(new CustomProvider
        {
            Id = Guid.Parse("6ea7e0b6-ee41-4791-802c-ea0625ee8d6b"),
            Type = CustomProviderType.OAuth2,
            Identifier = "custom:customauth",
            Name = "Custom Auth Provider",
            ClientId = "client-id",
            PkceEnabled = true,
            Enabled = true,
            AuthorizationUrl = new Uri("https://provider.example.com/oauth/authorize"),
            TokenUrl = new Uri("https://provider.example.com/oauth/token"),
            UserInfoUrl = new Uri("https://provider.example.com/oauth/userinfo"),
            JwksUri = new Uri("https://provider.example.com/oauth/jwks"),
            CreatedAt = new DateTime(2026, 10, 29, 13, 20, 39, 870, DateTimeKind.Utc).AddTicks(4000),
            UpdatedAt = new DateTime(2026, 10, 29, 13, 20, 39, 870, DateTimeKind.Utc).AddTicks(4000),
        }, "the auth server answers 201 with the stored provider, as in its CustomOAuthProviderSchema");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldReturnRegisteredProvider_GivenOidcProvider()
    {
        this.AnswerWith(CustomProviderResponses.Oidc);
        (await this.Admin.CreateCustomProviderAsync(OAuth2Provider)).Should().BeEquivalentTo(new CustomProvider
        {
            Id = Guid.Parse("1f0c6b8e-3d1a-4c55-9a43-5f1a2b7c9d10"),
            Type = CustomProviderType.Oidc,
            Identifier = "custom:mycompany",
            Name = "My Company SSO",
            ClientId = "client-id",
            AcceptableClientIds = new[] { "ios-client-id" },
            Scopes = new[] { "openid", "email", "profile" },
            PkceEnabled = true,
            AttributeMapping = new Dictionary<string, object> { ["email"] = "user_email", ["email_verified"] = true },
            CustomClaimsAllowlist = new[] { "groups" },
            AuthorizationParameters = new Dictionary<string, string> { ["prompt"] = "consent" },
            Enabled = true,
            EmailOptional = true,
            Issuer = new Uri("https://accounts.mycompany.com"),
            DiscoveryUrl = new Uri("https://accounts.mycompany.com/.well-known/openid-configuration"),
            SkipNonceCheck = true,
            DiscoveryDocument = new OidcDiscoveryDocument
            {
                Issuer = new Uri("https://accounts.mycompany.com"),
                AuthorizationEndpoint = new Uri("https://accounts.mycompany.com/authorize"),
                TokenEndpoint = new Uri("https://accounts.mycompany.com/token"),
                UserInfoEndpoint = new Uri("https://accounts.mycompany.com/userinfo"),
                JwksUri = new Uri("https://accounts.mycompany.com/jwks"),
                ScopesSupported = new[] { "openid", "email" },
                ResponseTypesSupported = new[] { "code" },
                GrantTypesSupported = new[] { "authorization_code" },
                SubjectTypesSupported = new[] { "public" },
            },
            CreatedAt = new DateTime(2026, 10, 29, 13, 20, 39, 870, DateTimeKind.Utc).AddTicks(4000),
            UpdatedAt = new DateTime(2026, 10, 29, 13, 25, 0, 500, DateTimeKind.Utc),
        }, "the auth server returns the OIDC discovery document it validated under discovery_document");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldReturnEmptyCollections_GivenNullCollectionsInResponse()
    {
        this.AnswerWith(CustomProviderResponses.SparseOidc);
        (await this.Admin.CreateCustomProviderAsync(OAuth2Provider)).Should().BeEquivalentTo(new CustomProvider
        {
            Id = Guid.Parse("2a7e4c1d-8b3f-4e60-b1d2-6c3e4f5a6b70"),
            Type = CustomProviderType.Oidc,
            Identifier = "custom:sparse",
            Name = "Sparse",
            ClientId = "client-id",
            Issuer = new Uri("https://sparse.example.com"),
            DiscoveryDocument = new OidcDiscoveryDocument
            {
                Issuer = new Uri("https://sparse.example.com"),
                AuthorizationEndpoint = new Uri("https://sparse.example.com/authorize"),
                TokenEndpoint = new Uri("https://sparse.example.com/token"),
                JwksUri = new Uri("https://sparse.example.com/jwks"),
            },
            CreatedAt = new DateTime(2026, 10, 29, 13, 20, 39, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 29, 13, 20, 39, DateTimeKind.Utc),
        }, "collections are exposed read-only and never null, whatever the server sends");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldThrowGotrueException_GivenUnrecognizedProviderType()
    {
        this.AnswerWith(CustomProviderResponses.OAuth2.Replace("\"oauth2\"", "\"saml\""));
        var create = () => this.Admin.CreateCustomProviderAsync(OAuth2Provider);
        await create.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*unrecognized custom provider type 'saml'*",
            "CustomProviderType only models the oauth2 and oidc types the auth server defines");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldThrowGotrueException_GivenNullResponseBody()
    {
        this.AnswerWith("null");
        var create = () => this.Admin.CreateCustomProviderAsync(OAuth2Provider);
        await create.Should().ThrowExactlyAsync<GotrueException>().WithMessage("*returned no custom provider*",
            "a 201 always carries the created provider; anything else is a broken response, not a provider");
    }

    [TestMethod]
    public void CreateCustomProviderAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.AnswerWith(CustomProviderResponses.OAuth2);
        BlockingCaller.Completes(() => this.Admin.CreateCustomProviderAsync(OAuth2Provider)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldThrowArgumentNull_GivenNullProvider()
    {
        var create = () => this.Admin.CreateCustomProviderAsync(null!);
        await create.Should().ThrowExactlyAsync<ArgumentNullException>().WithParameterName("provider");
    }

    [TestMethod]
    public async Task CreateCustomProviderAsync_ShouldNotSendRequest_GivenCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var create = () => this.Admin.CreateCustomProviderAsync(OAuth2Provider, cancellationToken: cancelled.Token);
        await create.Should().ThrowAsync<OperationCanceledException>();
        this.server.CountReceivedRequests().Should().Be(0, "a cancelled token must stop the request before it reaches the wire");
    }

    private AdminClient Admin => new(ServiceKey, new ClientOptions { Url = this.server.Url });

    private void AnswerWith(string body) =>
        this.server.Given(Request.Create().WithPath("/admin/custom-providers").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
