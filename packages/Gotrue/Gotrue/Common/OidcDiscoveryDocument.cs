using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// The OpenID Connect discovery document the auth server fetched and validated for an OIDC provider.
/// </summary>
public sealed record OidcDiscoveryDocument
{
    /// <summary>The issuer the document declares.</summary>
    public Uri Issuer { get; init; } = null!;

    /// <summary>The authorization endpoint.</summary>
    public Uri AuthorizationEndpoint { get; init; } = null!;

    /// <summary>The token endpoint.</summary>
    public Uri TokenEndpoint { get; init; } = null!;

    /// <summary>The userinfo endpoint, when the provider publishes one.</summary>
    public Uri? UserInfoEndpoint { get; init; }

    /// <summary>The JSON Web Key Set the provider signs its tokens with.</summary>
    public Uri JwksUri { get; init; } = null!;

    /// <summary>The scopes the provider supports.</summary>
    public IReadOnlyList<string> ScopesSupported { get; init; } = Array.Empty<string>();

    /// <summary>The OAuth response types the provider supports.</summary>
    public IReadOnlyList<string> ResponseTypesSupported { get; init; } = Array.Empty<string>();

    /// <summary>The OAuth grant types the provider supports.</summary>
    public IReadOnlyList<string> GrantTypesSupported { get; init; } = Array.Empty<string>();

    /// <summary>The subject identifier types the provider supports.</summary>
    public IReadOnlyList<string> SubjectTypesSupported { get; init; } = Array.Empty<string>();
}
