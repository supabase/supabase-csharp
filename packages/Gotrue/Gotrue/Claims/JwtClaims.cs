using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Claims;

/// <summary>
/// Standard JWT claims. Anything else is available in <see cref="AdditionalClaims" />.
/// </summary>
public sealed record JwtClaims
{
    /// <summary>Issuer of the token.</summary>
    [JsonPropertyName("iss")]
    public string? Iss { get; init; }

    /// <summary>The user id.</summary>
    [JsonPropertyName("sub")]
    public string? Sub { get; init; }

    /// <summary>Audiences the token was issued for, one or many.</summary>
    [JsonPropertyName("aud")]
    [JsonConverter(typeof(AudienceConverter))]
    public IReadOnlyList<string> Aud { get; init; } = Array.Empty<string>();

    /// <summary>Expiry, as seconds since the Unix epoch.</summary>
    [JsonPropertyName("exp")]
    public long? Exp { get; init; }

    /// <summary>Issued at, Unix seconds.</summary>
    [JsonPropertyName("iat")]
    public long? Iat { get; init; }

    /// <summary>Postgres role the token authenticates as.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>Authenticator assurance level reached by the session.</summary>
    [JsonPropertyName("aal")]
    public string? Aal { get; init; }

    /// <summary>Id of the session the token belongs to.</summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>Every other claim carried by the token, including custom ones.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalClaims { get; init; } = new();
}
