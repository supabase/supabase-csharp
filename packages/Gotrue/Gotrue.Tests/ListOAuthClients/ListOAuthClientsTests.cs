#region

using System;
using System.Collections.Generic;
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

namespace Gotrue.Tests.ListOAuthClients;

/// <summary>
///     End-to-end listing of OAuth clients against the live stack. Each test registers its own clients through the
///     admin API directly and removes them after it; other tests' clients may be listed too, so assertions look for
///     the test's own ids.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ListOAuthClientsTests : AdminFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);

    private readonly List<Guid> seeded = new();

    [TestCleanup]
    public async Task RemoveClients()
    {
        foreach (var clientId in this.seeded)
            (await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/oauth/clients/{clientId}")).Dispose();
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldListEveryRegisteredClient()
    {
        var confidentialName = $"e2e-{RandomString(12)}";
        var publicName = $"e2e-{RandomString(12)}";
        var confidential = await this.SeedClient(new { client_name = confidentialName, redirect_uris = new[] { "https://example.com/callback" } });
        var publicClient = await this.SeedClient(new { client_name = publicName, redirect_uris = new[] { "http://localhost:3000/callback" }, token_endpoint_auth_method = "none" });
        var listed = await this.Admin.ListOAuthClientsAsync();
        listed.Should().Contain(client => client.ClientId == confidential && client.Name == confidentialName && client.Type == OAuthClientType.Confidential,
            "the auth server lists every client that is not deleted");
        listed.Should().Contain(client => client.ClientId == publicClient && client.Name == publicName && client.Type == OAuthClientType.Public);
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldNotListDeletedClient()
    {
        var clientId = await this.SeedClient(new { client_name = $"e2e-{RandomString(12)}", redirect_uris = new[] { "https://example.com/callback" } });
        (await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/oauth/clients/{clientId}")).Dispose();
        (await this.Admin.ListOAuthClientsAsync()).Should().NotContain(client => client.ClientId == clientId,
            "the auth server soft-deletes a client and leaves it out of the list");
    }

    [TestMethod]
    public async Task ListOAuthClientsAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var list = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).ListOAuthClientsAsync();
        await list.Should().ThrowAsync<GotrueException>().Where(failure => failure.Reason == FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim");
    }

    private async Task<Guid> SeedClient(object body)
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/oauth/clients", JsonSerializer.Serialize(body));
        created.EnsureSuccessStatusCode();
        var clientId = Guid.Parse(JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("client_id").GetString()!);
        this.seeded.Add(clientId);
        return clientId;
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
