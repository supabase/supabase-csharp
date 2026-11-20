using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>An OAuth grant representing a user's authorization of a client.</summary>
public sealed record OAuthGrant
{
    /// <summary>OAuth client information.</summary>
    [JsonPropertyName("client")]
    public OAuthAuthorizationClient Client { get; init; } = new();

    /// <summary>Scopes granted to this client.</summary>
    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; init; } = new();

    /// <summary>Timestamp when the grant was created.</summary>
    [JsonPropertyName("granted_at")]
    public DateTimeOffset GrantedAt { get; init; }
}
