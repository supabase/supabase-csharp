#region

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Gotrue.Tests.Admin;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Common;

/// <summary>
///     Base for end-to-end custom provider tests against the live stack: each test works on its own provider
///     identifier, seeded and removed through the admin API directly so the assertions only exercise the SDK call
///     under test.
/// </summary>
public abstract class CustomProviderAdminFixture : AdminFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);

    protected string Identifier { get; } = $"custom:{RandomString(12)}";

    [TestCleanup]
    public async Task RemoveProvider() => (await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/custom-providers/{this.Identifier}")).Dispose();

    protected async Task SeedProvider()
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/custom-providers", $$"""
            {
              "provider_type": "oauth2",
              "identifier": "{{this.Identifier}}",
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
