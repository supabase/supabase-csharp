using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Exceptions;

namespace Supabase.Gotrue.Common;

internal sealed record CustomProviderResponse
{
    private static readonly IReadOnlyDictionary<string, CustomProviderType> TypesByWireName =
        Enum.GetValues(typeof(CustomProviderType)).Cast<CustomProviderType>()
            .ToDictionary(type => Core.Helpers.GetMappedToAttr(type).Mapping);

    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("provider_type")]
    public string ProviderType { get; init; } = null!;

    [JsonPropertyName("identifier")]
    public string Identifier { get; init; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = null!;

    [JsonPropertyName("acceptable_client_ids")]
    public IReadOnlyList<string>? AcceptableClientIds { get; init; }

    [JsonPropertyName("scopes")]
    public IReadOnlyList<string>? Scopes { get; init; }

    [JsonPropertyName("pkce_enabled")]
    public bool PkceEnabled { get; init; }

    [JsonPropertyName("attribute_mapping")]
    public IReadOnlyDictionary<string, object>? AttributeMapping { get; init; }

    [JsonPropertyName("custom_claims_allowlist")]
    public IReadOnlyList<string>? CustomClaimsAllowlist { get; init; }

    [JsonPropertyName("authorization_params")]
    public IReadOnlyDictionary<string, string>? AuthorizationParameters { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("email_optional")]
    public bool EmailOptional { get; init; }

    [JsonPropertyName("issuer")]
    public Uri? Issuer { get; init; }

    [JsonPropertyName("discovery_url")]
    public Uri? DiscoveryUrl { get; init; }

    [JsonPropertyName("skip_nonce_check")]
    public bool SkipNonceCheck { get; init; }

    [JsonPropertyName("discovery_document")]
    public OidcDiscoveryDocumentResponse? DiscoveryDocument { get; init; }

    [JsonPropertyName("authorization_url")]
    public Uri? AuthorizationUrl { get; init; }

    [JsonPropertyName("token_url")]
    public Uri? TokenUrl { get; init; }

    [JsonPropertyName("userinfo_url")]
    public Uri? UserInfoUrl { get; init; }

    [JsonPropertyName("jwks_uri")]
    public Uri? JwksUri { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    internal static CustomProvider Read(CustomProviderResponse? response) =>
        response?.ToCustomProvider() ?? throw new GotrueException("The auth server returned no custom provider.");

    internal CustomProvider ToCustomProvider() =>
        new()
        {
            Id = this.Id,
            Type = ParseType(this.ProviderType),
            Identifier = this.Identifier,
            Name = this.Name,
            ClientId = this.ClientId,
            AcceptableClientIds = this.AcceptableClientIds ?? Array.Empty<string>(),
            Scopes = this.Scopes ?? Array.Empty<string>(),
            PkceEnabled = this.PkceEnabled,
            AttributeMapping = this.AttributeMapping ?? new Dictionary<string, object>(),
            CustomClaimsAllowlist = this.CustomClaimsAllowlist ?? Array.Empty<string>(),
            AuthorizationParameters = this.AuthorizationParameters ?? new Dictionary<string, string>(),
            Enabled = this.Enabled,
            EmailOptional = this.EmailOptional,
            Issuer = this.Issuer,
            DiscoveryUrl = this.DiscoveryUrl,
            SkipNonceCheck = this.SkipNonceCheck,
            DiscoveryDocument = this.DiscoveryDocument?.ToDiscoveryDocument(),
            AuthorizationUrl = this.AuthorizationUrl,
            TokenUrl = this.TokenUrl,
            UserInfoUrl = this.UserInfoUrl,
            JwksUri = this.JwksUri,
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt,
        };

    private static CustomProviderType ParseType(string wireName) =>
        TypesByWireName.TryGetValue(wireName, out var type)
            ? type
            : throw new GotrueException($"The auth server returned an unrecognized custom provider type '{wireName}'.");
}
