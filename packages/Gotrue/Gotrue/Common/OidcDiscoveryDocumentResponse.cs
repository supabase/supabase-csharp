using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Common;

internal sealed record OidcDiscoveryDocumentResponse
{
    [JsonPropertyName("issuer")]
    public Uri Issuer { get; init; } = null!;

    [JsonPropertyName("authorization_endpoint")]
    public Uri AuthorizationEndpoint { get; init; } = null!;

    [JsonPropertyName("token_endpoint")]
    public Uri TokenEndpoint { get; init; } = null!;

    [JsonPropertyName("userinfo_endpoint")]
    public Uri? UserInfoEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public Uri JwksUri { get; init; } = null!;

    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string>? ScopesSupported { get; init; }

    [JsonPropertyName("response_types_supported")]
    public IReadOnlyList<string>? ResponseTypesSupported { get; init; }

    [JsonPropertyName("grant_types_supported")]
    public IReadOnlyList<string>? GrantTypesSupported { get; init; }

    [JsonPropertyName("subject_types_supported")]
    public IReadOnlyList<string>? SubjectTypesSupported { get; init; }

    internal OidcDiscoveryDocument ToDiscoveryDocument() =>
        new()
        {
            Issuer = this.Issuer,
            AuthorizationEndpoint = this.AuthorizationEndpoint,
            TokenEndpoint = this.TokenEndpoint,
            UserInfoEndpoint = this.UserInfoEndpoint,
            JwksUri = this.JwksUri,
            ScopesSupported = this.ScopesSupported ?? Array.Empty<string>(),
            ResponseTypesSupported = this.ResponseTypesSupported ?? Array.Empty<string>(),
            GrantTypesSupported = this.GrantTypesSupported ?? Array.Empty<string>(),
            SubjectTypesSupported = this.SubjectTypesSupported ?? Array.Empty<string>(),
        };
}
