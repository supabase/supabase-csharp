using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gotrue.Tests.Support;
using static Gotrue.Tests.TestUtils;

namespace Gotrue.Tests.AuthorizationOAuth;

/// <summary>Base fixture for OAuth authorization end-to-end tests against the local Supabase stack.</summary>
public abstract class AuthorizationOAuthFixture : AuthClientFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);
    private string? clientId;

    protected string RedirectUri { get; } = "https://oauth-client.example/callback";

    protected string ClientId => this.clientId ?? throw new InvalidOperationException("The OAuth client has not been registered.");

    protected async Task CreateOAuthClient()
    {
        this.Client.Options.Headers["Origin"] = "http://127.0.0.1:3000";
        using var response = await SendAdminRequest(
            HttpMethod.Post,
            $"{TestClients.CliAuthUrl}/admin/oauth/clients",
            $$"""
            {
              "client_name": "Gotru E2E {{Guid.NewGuid():N}}",
              "redirect_uris": ["{{this.RedirectUri}}"],
              "client_type": "public"
            }
            """
        );

        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NotImplemented)
        {
            Assert.Inconclusive("Enable the Supabase Auth OAuth server in the local stack to run these E2E tests.");
        }

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        this.clientId = payload.RootElement.GetProperty("client_id").GetString();
        this.clientId.Should().NotBeNullOrEmpty();
    }

    protected async Task<string> StartAuthorizationRequest(string scope = "openid profile email")
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var state = Guid.NewGuid().ToString("N");
        var authorizeUrl = $"{TestClients.CliAuthUrl}/oauth/authorize?client_id={Uri.EscapeDataString(this.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(this.RedirectUri)}&response_type=code&scope={Uri.EscapeDataString(scope)}" +
            $"&state={Uri.EscapeDataString(state)}&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256";

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, authorizeUrl);
        request.Headers.Add("apikey", TestClients.CliAnonKey);
        request.Headers.Add("Origin", "http://127.0.0.1:3000");
        using var response = await http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        var location = response.Headers.Location;
        location.Should().NotBeNull();
        var authorizationId = ParseQuery(location!.Query).GetValueOrDefault("authorization_id");
        authorizationId.Should().NotBeNullOrEmpty("the authorize endpoint should redirect with an authorization ID");
        return authorizationId!;
    }

    protected async Task DeleteOAuthClient()
    {
        if (this.clientId is null)
        {
            return;
        }

        using var response = await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/oauth/clients/{Uri.EscapeDataString(this.clientId)}");
        response.EnsureSuccessStatusCode();
    }

    [TestCleanup]
    public async Task CleanupOAuthClient() => await this.DeleteOAuthClient();

    private static async Task<HttpResponseMessage> SendAdminRequest(HttpMethod method, string url, string? json = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", TestClients.CliAnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await AdminApi.SendAsync(request);
    }

    private static System.Collections.Generic.Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty);
}
