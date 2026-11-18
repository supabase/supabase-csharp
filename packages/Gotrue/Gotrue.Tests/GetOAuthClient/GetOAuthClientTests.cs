#region

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Admin;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.GetOAuthClient;

/// <summary>
///     End-to-end retrieval of an OAuth client against the live stack. Each test registers its own client through the
///     admin API directly, so the assertions only exercise the SDK call under test.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class GetOAuthClientTests : AdminFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);

    private Guid? seeded;

    [TestCleanup]
    public async Task RemoveClient()
    {
        if (this.seeded is { } clientId)
            (await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/oauth/clients/{clientId}")).Dispose();
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldReturnClientWithoutSecret_GivenConfidentialClient()
    {
        var name = $"e2e-{RandomString(12)}";
        var clientId = await this.SeedClient(new { client_name = name, redirect_uris = new[] { "https://example.com/callback" }, client_uri = "https://example.com" });
        (await this.Admin.GetOAuthClientAsync(clientId)).Should().BeEquivalentTo(new
        {
            ClientId = clientId,
            Name = name,
            Type = OAuthClientType.Confidential,
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretBasic,
            RegistrationType = OAuthClientRegistrationType.Manual,
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode, OAuthClientGrantType.RefreshToken },
            ClientUri = new Uri("https://example.com"),
        }, "the auth server returns what the client was registered with, and never its secret");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldReturnClient_GivenPublicClient()
    {
        var clientId = await this.SeedClient(new { client_name = $"e2e-{RandomString(12)}", client_type = "public", redirect_uris = new[] { "http://localhost:3000/callback" } });
        (await this.Admin.GetOAuthClientAsync(clientId)).Should().BeEquivalentTo(new
        {
            Type = OAuthClientType.Public,
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.None,
        }, "the auth server infers the none authentication method for a public client");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        var get = () => this.Admin.GetOAuthClientAsync(Guid.NewGuid());
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
                "the auth server answers 404 oauth_client_not_found when no client has the id");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenDeletedClient()
    {
        var clientId = await this.SeedClient(new { client_name = $"e2e-{RandomString(12)}", redirect_uris = new[] { "https://example.com/callback" } });
        (await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/oauth/clients/{clientId}")).Dispose();
        var get = () => this.Admin.GetOAuthClientAsync(clientId);
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
                "the auth server soft-deletes a client and answers 404 for it afterwards");
    }

    [TestMethod]
    public async Task GetOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var clientId = await this.SeedClient(new { client_name = $"e2e-{RandomString(12)}", redirect_uris = new[] { "https://example.com/callback" } });
        var get = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).GetOAuthClientAsync(clientId);
        await get.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private async Task<Guid> SeedClient(object body)
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/oauth/clients", JsonSerializer.Serialize(body));
        created.EnsureSuccessStatusCode();
        this.seeded = Guid.Parse(JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("client_id").GetString()!);
        return this.seeded.Value;
    }

    private static async Task<HttpResponseMessage> SendAdminRequest(HttpMethod method, string url, string? json = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", TestClients.CliAnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
        if (json != null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await AdminApi.SendAsync(request);
    }
}
