using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Claims;

/// <summary>
/// A single JSON Web Key published by the server at /.well-known/jwks.json.
/// </summary>
public sealed record Jwk
{
    /// <summary>Key type, "EC" or "RSA".</summary>
    [JsonPropertyName("kty")]
    public string? Kty { get; init; }

    /// <summary>Key id, matched against the "kid" of the JWT header.</summary>
    [JsonPropertyName("kid")]
    public string? Kid { get; init; }

    /// <summary>Signing algorithm.</summary>
    [JsonPropertyName("alg")]
    public string? Alg { get; init; }

    /// <summary>Intended use of the key, "sig" for signing keys.</summary>
    [JsonPropertyName("use")]
    public string? Use { get; init; }

    /// <summary>RSA modulus.</summary>
    [JsonPropertyName("n")]
    public string? N { get; init; }

    /// <summary>RSA exponent.</summary>
    [JsonPropertyName("e")]
    public string? E { get; init; }

    /// <summary>Curve name, "P-256".</summary>
    [JsonPropertyName("crv")]
    public string? Crv { get; init; }

    /// <summary>Elliptic curve x coordinate.</summary>
    [JsonPropertyName("x")]
    public string? X { get; init; }

    /// <summary>Elliptic curve y coordinate.</summary>
    [JsonPropertyName("y")]
    public string? Y { get; init; }
}
