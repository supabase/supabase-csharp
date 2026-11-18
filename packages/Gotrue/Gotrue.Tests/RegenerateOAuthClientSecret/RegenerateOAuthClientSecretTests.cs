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
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.RegenerateOAuthClientSecret;

/// <summary>
///     End-to-end secret regeneration of OAuth clients against the live stack. Each test registers its own client through
///     the admin API directly, so the assertions only exercise the SDK call under test.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RegenerateOAuthClientSecretTests : AdminFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);

    private Guid? seeded;

    [TestCleanup]
    public async Task RemoveClient()
    {
        if (this.seeded is { } clientId)
            (await SendAdminRequest(HttpMethod.Delete, ClientUrl(clientId))).Dispose();
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldReturnNewSecret_GivenConfidentialClient()
    {
        var (clientId, secret) = await this.SeedClient("client_secret_basic");
        var regenerated = await this.Admin.RegenerateOAuthClientSecretAsync(clientId);
        regenerated.Client.ClientId.Should().Be(clientId);
        regenerated.ClientSecret.Should().NotBeNullOrEmpty("the auth server shows the new secret in the response")
            .And.NotBe(secret, "regeneration replaces the previous secret");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowValidationFailed_GivenPublicClient()
    {
        var (clientId, _) = await this.SeedClient("none");
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(clientId);
        await regenerate.Should().ThrowAsync<GotrueException>().Where(failure => failure.ErrorCode == "validation_failed",
            "the auth server answers 400 validation_failed for a public client, which has no secret");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        var regenerate = () => this.Admin.RegenerateOAuthClientSecretAsync(Guid.NewGuid());
        await regenerate.Should().ThrowAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
            "the auth server answers 404 oauth_client_not_found when no client has the id");
    }

    [TestMethod]
    public async Task RegenerateOAuthClientSecretAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var (clientId, _) = await this.SeedClient("client_secret_basic");
        var regenerate = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).RegenerateOAuthClientSecretAsync(clientId);
        await regenerate.Should().ThrowAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private async Task<(Guid ClientId, string? Secret)> SeedClient(string authMethod)
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/oauth/clients", JsonSerializer.Serialize(new
        {
            client_name = $"e2e-{RandomString(12)}",
            redirect_uris = new[] { "https://example.com/callback" },
            token_endpoint_auth_method = authMethod,
        }));
        created.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        this.seeded = Guid.Parse(body.GetProperty("client_id").GetString()!);
        return (this.seeded.Value, body.TryGetProperty("client_secret", out var secret) ? secret.GetString() : null);
    }

    private static string ClientUrl(Guid clientId) => $"{TestClients.CliAuthUrl}/admin/oauth/clients/{clientId}";

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
