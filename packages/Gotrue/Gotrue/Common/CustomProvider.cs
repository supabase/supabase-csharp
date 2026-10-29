using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// A custom OAuth 2.0 or OpenID Connect provider registered on the project. The client secret is never returned.
/// </summary>
public sealed record CustomProvider
{
    /// <summary>The server-assigned id.</summary>
    public Guid Id { get; init; }

    /// <summary>The protocol the provider speaks.</summary>
    public CustomProviderType Type { get; init; }

    /// <summary>The identifier users sign in with, starting with <c>custom:</c> (for example <c>custom:mycompany</c>).</summary>
    public string Identifier { get; init; } = null!;

    /// <summary>The display name.</summary>
    public string Name { get; init; } = null!;

    /// <summary>The OAuth client id registered with the provider.</summary>
    public string ClientId { get; init; } = null!;

    /// <summary>Other client ids accepted when validating ID tokens.</summary>
    public IReadOnlyList<string> AcceptableClientIds { get; init; } = Array.Empty<string>();

    /// <summary>The scopes requested at sign-in. OIDC providers always include <c>openid</c>.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>Whether sign-in uses PKCE.</summary>
    public bool PkceEnabled { get; init; }

    /// <summary>How provider claims map onto user attributes.</summary>
    public IReadOnlyDictionary<string, object> AttributeMapping { get; init; } = new Dictionary<string, object>();

    /// <summary>Provider claims copied verbatim into the user's <c>custom_claims</c>.</summary>
    public IReadOnlyList<string> CustomClaimsAllowlist { get; init; } = Array.Empty<string>();

    /// <summary>Extra parameters sent with every authorization request.</summary>
    public IReadOnlyDictionary<string, string> AuthorizationParameters { get; init; } = new Dictionary<string, string>();

    /// <summary>Whether users can sign in with the provider.</summary>
    public bool Enabled { get; init; }

    /// <summary>Whether users without an email address can sign in.</summary>
    public bool EmailOptional { get; init; }

    /// <summary>The OIDC issuer. Null for OAuth 2.0 providers.</summary>
    public Uri? Issuer { get; init; }

    /// <summary>The OIDC discovery URL, when it differs from the issuer's well-known location.</summary>
    public Uri? DiscoveryUrl { get; init; }

    /// <summary>Whether the OIDC nonce check is skipped.</summary>
    public bool SkipNonceCheck { get; init; }

    /// <summary>The discovery document fetched from the issuer. Null for OAuth 2.0 providers.</summary>
    public OidcDiscoveryDocument? DiscoveryDocument { get; init; }

    /// <summary>The OAuth 2.0 authorization endpoint. Null for OIDC providers.</summary>
    public Uri? AuthorizationUrl { get; init; }

    /// <summary>The OAuth 2.0 token endpoint. Null for OIDC providers.</summary>
    public Uri? TokenUrl { get; init; }

    /// <summary>The OAuth 2.0 userinfo endpoint. Null for OIDC providers.</summary>
    public Uri? UserInfoUrl { get; init; }

    /// <summary>The JSON Web Key Set used to validate OAuth 2.0 provider tokens, when configured.</summary>
    public Uri? JwksUri { get; init; }

    /// <summary>When the provider was registered.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the provider was last changed.</summary>
    public DateTime UpdatedAt { get; init; }
}
