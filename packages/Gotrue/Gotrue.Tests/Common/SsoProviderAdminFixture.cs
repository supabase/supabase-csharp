#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Gotrue.Tests.Admin;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Common;

/// <summary>
///     Base for end-to-end SSO provider tests against the live stack: each test registers its own provider, with an
///     entity ID, domain and resource ID no other test uses, through the admin API directly so the assertions only
///     exercise the SDK call under test. A test that needs several providers registers the others with
///     <see cref="SeedAnotherProvider" />, each with an entity ID and domain of its own. Every provider is removed
///     after the test.
/// </summary>
public abstract class SsoProviderAdminFixture : AdminFixture
{
    private static readonly HttpClient AdminApi = new();
    private static readonly string ServiceKey = GenerateServiceRoleToken(TestClients.CliJwtSecret);

    private readonly List<Guid> seededProviders = new();

    protected string EntityId { get; } = UniqueEntityId();

    protected string Domain { get; } = UniqueDomain();

    [TestCleanup]
    public async Task RemoveProviders()
    {
        foreach (var provider in this.seededProviders)
            (await SendAdminRequest(HttpMethod.Delete, $"{TestClients.CliAuthUrl}/admin/sso/providers/{provider}")).Dispose();
    }

    protected Task<Guid> SeedProvider(string resourceId) => this.SeedProvider(resourceId, this.EntityId, this.Domain);

    protected Task<Guid> SeedAnotherProvider(string resourceId) =>
        this.SeedProvider(resourceId, UniqueEntityId(), UniqueDomain());

    private async Task<Guid> SeedProvider(string resourceId, string entityId, string domain)
    {
        using var created = await SendAdminRequest(HttpMethod.Post, $"{TestClients.CliAuthUrl}/admin/sso/providers", JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "saml",
            ["metadata_xml"] = MetadataXmlFor(entityId),
            ["resource_id"] = resourceId,
            ["domains"] = new[] { domain },
            ["name_id_format"] = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            ["attribute_mapping"] = new { keys = new { email = new { name = "mail", names = new[] { "email" } } } },
        }));
        created.EnsureSuccessStatusCode();
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        this.seededProviders.Add(id);
        return id;
    }

    protected string MetadataXml => MetadataXmlFor(this.EntityId);

    private static string MetadataXmlFor(string entityId) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Common", "Data", "saml_idp_metadata.xml"))
            .Replace("{{entity_id}}", entityId);

    private static string UniqueEntityId() => $"https://idp.{RandomString(12)}.example.com/saml";

    private static string UniqueDomain() => $"{RandomString(12)}.example.com";

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
