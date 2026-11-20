using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Common;

/// <summary>
/// An OAuth grant a client is allowed to use at the token endpoint.
/// </summary>
public enum OAuthClientGrantType
{
    /// <summary>Exchanges an authorization code for tokens.</summary>
    [MapTo("authorization_code")]
    AuthorizationCode,

    /// <summary>Exchanges a refresh token for new tokens.</summary>
    [MapTo("refresh_token")]
    RefreshToken,
}
