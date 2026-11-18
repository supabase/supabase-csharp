#region

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Admin;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.CreateOAuthClient;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.CreateOAuthClient;

/// <summary>
///     End-to-end registration of OAuth clients against the live stack. Every client a test registers is removed after it
///     through the admin API directly.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class CreateOAuthClientTests : AdminFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);

    private Guid? created;

    [TestCleanup]
    public async Task RemoveClient()
    {
        if (this.created is { } clientId)
            (await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/oauth/clients/{clientId}")).Dispose();
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldRegisterConfidentialClientWithSecret()
    {
        var name = $"e2e-{RandomString(12)}";
        var result = await this.Admin.CreateOAuthClientAsync(
            NewOAuthClient.Confidential(name, new[] { new Uri("https://example.com/callback") }, OAuthClientTokenEndpointAuthMethod.ClientSecretPost),
            new OAuthClientOptions { ClientUri = new Uri("https://example.com") });
        this.created = result.Client.ClientId;
        result.Should().BeEquivalentTo(new
        {
            Client = new
            {
                Name = name,
                Type = OAuthClientType.Confidential,
                TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretPost,
                RegistrationType = OAuthClientRegistrationType.Manual,
                RedirectUris = new[] { new Uri("https://example.com/callback") },
                GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode, OAuthClientGrantType.RefreshToken },
                ClientUri = new Uri("https://example.com"),
            },
        }, "the auth server registers an admin-created client as manual, with both grants by default");
        result.ClientSecret.Should().NotBeNullOrEmpty("the auth server generates a secret for a confidential client and shows it once");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldRegisterPublicClientWithoutSecret()
    {
        var result = await this.Admin.CreateOAuthClientAsync(
            NewOAuthClient.Public($"e2e-{RandomString(12)}", new[] { new Uri("http://localhost:3000/callback") }),
            new OAuthClientOptions { GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode } });
        this.created = result.Client.ClientId;
        result.Should().BeEquivalentTo(new
        {
            ClientSecret = (string?) null,
            Client = new
            {
                Type = OAuthClientType.Public,
                TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.None,
                GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode },
            },
        }, "the auth server generates no secret for a public client");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowValidationFailed_GivenRedirectUriTheServerRejects()
    {
        var create = () => this.Admin.CreateOAuthClientAsync(NewOAuthClient.Public($"e2e-{RandomString(12)}", new[] { new Uri("http://example.com/callback") }));
        await create.Should().ThrowAsync<GotrueException>().Where(failure => failure.ErrorCode == "validation_failed",
            "the auth server only accepts a plain HTTP redirect URI for localhost");
    }

    [TestMethod]
    public async Task CreateOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var create = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey)
            .CreateOAuthClientAsync(NewOAuthClient.Public($"e2e-{RandomString(12)}", new[] { new Uri("https://example.com/callback") }));
        await create.Should().ThrowAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private static async Task<HttpResponseMessage> SendAdminRequest(HttpMethod method, string url)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", TestClients.CliAnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
        return await AdminApi.SendAsync(request);
    }
}
