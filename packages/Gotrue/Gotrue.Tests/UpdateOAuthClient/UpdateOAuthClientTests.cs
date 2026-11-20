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
using Supabase.Gotrue.UpdateOAuthClient;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.UpdateOAuthClient;

/// <summary>
///     End-to-end update of an OAuth client against the live stack. Each test registers its own client through the
///     admin API directly, so the assertions only exercise the SDK call under test.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class UpdateOAuthClientTests : AdminFixture
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
    public async Task UpdateOAuthClientAsync_ShouldChangeOnlyTheMembersSet()
    {
        var name = $"e2e-{RandomString(12)}";
        var clientId = await this.SeedClient("confidential");
        (await this.Admin.UpdateOAuthClientAsync(clientId, new OAuthClientChanges { Name = name })).Should().BeEquivalentTo(new
        {
            ClientId = clientId,
            Name = name,
            Type = OAuthClientType.Confidential,
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode, OAuthClientGrantType.RefreshToken },
        }, "the auth server updates only the fields present in the request");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldChangeEveryMemberSet()
    {
        var clientId = await this.SeedClient("confidential");
        (await this.Admin.UpdateOAuthClientAsync(clientId, new OAuthClientChanges
        {
            RedirectUris = new[] { new Uri("https://example.com/other"), new Uri("http://localhost:3000/callback") },
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretPost,
            Options = new OAuthClientOptions
            {
                GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode },
                ClientUri = new Uri("https://example.com"),
                LogoUri = new Uri("https://example.com/logo.png"),
            },
        })).Should().BeEquivalentTo(new
        {
            RedirectUris = new[] { new Uri("https://example.com/other"), new Uri("http://localhost:3000/callback") },
            TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.ClientSecretPost,
            GrantTypes = new[] { OAuthClientGrantType.AuthorizationCode },
            ClientUri = new Uri("https://example.com"),
            LogoUri = new Uri("https://example.com/logo.png"),
        });
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowValidationFailed_GivenAuthMethodThatDoesNotSuitClientType()
    {
        var clientId = await this.SeedClient("confidential");
        var update = () => this.Admin.UpdateOAuthClientAsync(clientId, new OAuthClientChanges { TokenEndpointAuthMethod = OAuthClientTokenEndpointAuthMethod.None });
        await update.Should().ThrowAsync<GotrueException>().Where(failure => failure.ErrorCode == "validation_failed",
            "the auth server only accepts the none method for a public client");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        var update = () => this.Admin.UpdateOAuthClientAsync(Guid.NewGuid(), new OAuthClientChanges { Name = "x" });
        await update.Should().ThrowAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
            "the auth server answers 404 oauth_client_not_found when no client has the id");
    }

    [TestMethod]
    public async Task UpdateOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var clientId = await this.SeedClient("confidential");
        var update = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).UpdateOAuthClientAsync(clientId, new OAuthClientChanges { Name = "x" });
        await update.Should().ThrowAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private async Task<Guid> SeedClient(string clientType)
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/oauth/clients", JsonSerializer.Serialize(new
        {
            client_name = $"e2e-{RandomString(12)}",
            client_type = clientType,
            redirect_uris = new[] { "https://example.com/callback" },
        }));
        created.EnsureSuccessStatusCode();
        this.seeded = Guid.Parse(JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("client_id").GetString()!);
        return this.seeded.Value;
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
