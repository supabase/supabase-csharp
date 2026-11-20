using System.Text.Json.Serialization;

namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>User information associated with an OAuth authorization.</summary>
public sealed record OAuthAuthorizationUser
{
    /// <summary>User ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>User email.</summary>
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
}
