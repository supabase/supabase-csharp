using System.Text.Json.Serialization;

namespace Supabase.Gotrue.AuthorizationOAuth;

/// <summary>Contains either OAuth consent details or an immediate redirect.</summary>
[JsonConverter(typeof(OAuthAuthorizationResponseConverter))]
public sealed record OAuthAuthorizationResponse
{
    /// <summary>Consent details when the user must make a decision.</summary>
    public OAuthAuthorizationDetails? Details { get; init; }

    /// <summary>Immediate redirect when consent has already been granted.</summary>
    public OAuthRedirect? Redirect { get; init; }
}
