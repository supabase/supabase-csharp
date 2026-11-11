using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.Common;

internal sealed record SsoProviderResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("resource_id")]
    public string? ResourceId { get; init; }

    [JsonPropertyName("disabled")]
    public bool? Disabled { get; init; }

    [JsonPropertyName("saml")]
    public SamlConfigurationResponse? Saml { get; init; }

    [JsonPropertyName("domains")]
    public IReadOnlyList<SsoDomainResponse>? Domains { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    internal static SsoProvider Read(SsoProviderResponse? response) =>
        response?.ToSsoProvider() ?? throw new GotrueException("The auth server returned no SSO provider.");

    internal SsoProvider ToSsoProvider() =>
        new()
        {
            Id = this.Id,
            ResourceId = this.ResourceId,
            Enabled = this.Disabled != true,
            Saml = this.ReadSamlConfiguration(),
            Domains = (this.Domains ?? Array.Empty<SsoDomainResponse>()).Select(domain => domain.Domain).ToList(),
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt,
        };

    private SamlConfiguration ReadSamlConfiguration() =>
        this.Saml?.ToSamlConfiguration() ?? throw new GotrueException("The auth server returned an SSO provider without its SAML configuration.");
}
