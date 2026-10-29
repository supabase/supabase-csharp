using Supabase.Core.Attributes;

namespace Supabase.Gotrue.Common;

/// <summary>
/// The protocol a custom provider speaks.
/// </summary>
public enum CustomProviderType
{
    /// <summary>A plain OAuth 2.0 provider, configured with its authorization, token and userinfo endpoints.</summary>
    [MapTo("oauth2")]
    OAuth2,

    /// <summary>An OpenID Connect provider, configured from its issuer's discovery document.</summary>
    [MapTo("oidc")]
    Oidc,
}
