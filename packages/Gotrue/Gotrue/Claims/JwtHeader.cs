using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Claims;

/// <summary>
/// The decoded header of a JWT.
/// </summary>
public sealed record JwtHeader
{
    /// <summary>Signing algorithm.</summary>
    [JsonPropertyName("alg")]
    public string? Alg { get; init; }

    /// <summary>Id of the key that signed the token, when present.</summary>
    [JsonPropertyName("kid")]
    public string? Kid { get; init; }

    /// <summary>Token type.</summary>
    [JsonPropertyName("typ")]
    public string? Typ { get; init; }
}
