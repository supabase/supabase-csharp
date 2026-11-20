using System.Text.Json.Serialization;

namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>OAuth authorization details presented when user consent is required.</summary>
public sealed record OAuthAuthorizationDetails
{
    /// <summary>The authorization ID used to approve or deny the request.</summary>
    [JsonPropertyName("authorization_id")]
    public string AuthorizationId { get; init; } = string.Empty;

    /// <summary>The registered redirect URI, without query parameters.</summary>
    [JsonPropertyName("redirect_uri")]
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>The OAuth client requesting authorization.</summary>
    [JsonPropertyName("client")]
    public OAuthAuthorizationClient Client { get; init; } = new();

    /// <summary>The user associated with the authorization.</summary>
    [JsonPropertyName("user")]
    public OAuthAuthorizationUser User { get; init; } = new();

    /// <summary>Space-separated list of requested scopes.</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;
}
