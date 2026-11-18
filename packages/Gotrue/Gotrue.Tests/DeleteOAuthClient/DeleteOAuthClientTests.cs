#region

using System;
using System.Net;
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

namespace Gotrue.Tests.DeleteOAuthClient;

/// <summary>
///     End-to-end removal of an OAuth client against the live stack. Each test registers its own client through the
///     admin API directly, so the assertions only exercise the SDK call under test.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class DeleteOAuthClientTests : AdminFixture
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
    public async Task DeleteOAuthClientAsync_ShouldRemoveClient()
    {
        var clientId = await this.SeedClient();
        await this.Admin.DeleteOAuthClientAsync(clientId);
        using var fetched = await SendAdminRequest(HttpMethod.Get, ClientUrl(clientId));
        fetched.StatusCode.Should().Be(HttpStatusCode.NotFound, "a deleted client is no longer retrievable by its id");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenClientAlreadyDeleted()
    {
        var clientId = await this.SeedClient();
        await this.Admin.DeleteOAuthClientAsync(clientId);
        var deleteAgain = () => this.Admin.DeleteOAuthClientAsync(clientId);
        await deleteAgain.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
                "the auth server soft-deletes a client and answers 404 oauth_client_not_found for it afterwards");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldThrowOAuthClientNotFound_GivenUnknownClient()
    {
        var delete = () => this.Admin.DeleteOAuthClientAsync(Guid.NewGuid());
        await delete.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.OAuthClientNotFound,
                "the auth server answers 404 oauth_client_not_found when no client has the id");
    }

    [TestMethod]
    public async Task DeleteOAuthClientAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var clientId = await this.SeedClient();
        var delete = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).DeleteOAuthClientAsync(clientId);
        await delete.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private async Task<Guid> SeedClient()
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/oauth/clients", JsonSerializer.Serialize(new
        {
            client_name = $"e2e-{RandomString(12)}",
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
