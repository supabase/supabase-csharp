using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Claims;

/// <summary>
/// Public keys used to verify JWT signatures.
/// </summary>
public sealed record Jwks
{
    /// <summary>The published keys, empty when the server signs symmetrically.</summary>
    [JsonPropertyName("keys")]
    public IReadOnlyList<Jwk> Keys { get; init; } = Array.Empty<Jwk>();
}
