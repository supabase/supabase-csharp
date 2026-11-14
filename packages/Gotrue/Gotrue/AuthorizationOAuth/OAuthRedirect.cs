using System.Text.Json.Serialization;

namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>OAuth redirect response after consent or when consent is already granted.</summary>
public sealed record OAuthRedirect
{
    /// <summary>Complete URL to redirect the user to the OAuth client.</summary>
    [JsonPropertyName("redirect_url")]
    public string RedirectUrl { get; init; } = string.Empty;
}
