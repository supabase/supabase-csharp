using System.Text.Json.Serialization;

namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>OAuth client details in an authorization request.</summary>
public sealed record OAuthAuthorizationClient
{
    /// <summary>Unique identifier for the OAuth client.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable name of the OAuth client.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>URI of the OAuth client's website.</summary>
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;

    /// <summary>URI of the OAuth client's logo.</summary>
    [JsonPropertyName("logo_uri")]
    public string LogoUri { get; init; } = string.Empty;
}
