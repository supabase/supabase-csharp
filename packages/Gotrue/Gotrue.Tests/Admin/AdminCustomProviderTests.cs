#region

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     End-to-end custom OAuth/OIDC provider administration against the live stack. Each test works on its own
///     provider, seeded and removed through the admin API directly so the assertions only exercise the SDK call
///     under test.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class AdminCustomProviderTests : AdminFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);

    private readonly string identifier = $"custom:{RandomString(12)}";

    [TestCleanup]
    public async Task RemoveSeededProvider() => (await SendAdminRequest(HttpMethod.Delete, this.ProviderUrl)).Dispose();

    [TestMethod]
    public async Task DeleteCustomProviderAsync_ShouldRemoveProvider()
    {
        await this.SeedProvider();
        await this.Admin.DeleteCustomProviderAsync(this.identifier);
        using var lookup = await SendAdminRequest(HttpMethod.Get, this.ProviderUrl);
        lookup.StatusCode.Should().Be(HttpStatusCode.NotFound, "a deleted provider is no longer retrievable by its identifier");
    }

    [TestMethod]
    public async Task DeleteCustomProviderAsync_ShouldThrowCustomProviderNotFound_GivenUnknownIdentifier()
    {
        var delete = () => this.Admin.DeleteCustomProviderAsync(this.identifier);
        await delete.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.CustomProviderNotFound,
                "the auth server answers 404 custom_provider_not_found when no provider has the identifier");
    }

    [TestMethod]
    public async Task DeleteCustomProviderAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        await this.SeedProvider();
        var delete = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).DeleteCustomProviderAsync(this.identifier);
        await delete.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
                "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private string ProviderUrl => $"{TestClients.CliAuthUrl}/admin/custom-providers/{this.identifier}";

    private async Task SeedProvider()
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/custom-providers", $$"""
            {
              "provider_type": "oauth2",
              "identifier": "{{this.identifier}}",
              "name": "E2E provider",
              "client_id": "client-id",
              "client_secret": "client-secret",
              "authorization_url": "https://example.com/authorize",
              "token_url": "https://example.com/token",
              "userinfo_url": "https://example.com/userinfo"
            }
            """);
        created.EnsureSuccessStatusCode();
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
