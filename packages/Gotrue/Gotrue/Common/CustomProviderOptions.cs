using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// Optional settings of a custom provider. A member left null is not sent: the server applies its default when
/// the provider is created.
/// </summary>
public sealed record CustomProviderOptions
{
    /// <summary>Other client ids accepted when validating ID tokens.</summary>
    public IReadOnlyList<string>? AcceptableClientIds { get; init; }

    /// <summary>The scopes requested at sign-in. The server adds <c>openid</c> for OIDC providers.</summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>Whether sign-in uses PKCE. The server default is <c>true</c>.</summary>
    public bool? PkceEnabled { get; init; }

    /// <summary>How provider claims map onto user attributes. Protected fields such as <c>role</c> or <c>id</c> are rejected.</summary>
    public IReadOnlyDictionary<string, object>? AttributeMapping { get; init; }

    /// <summary>Provider claims copied verbatim into the user's <c>custom_claims</c>, such as <c>groups</c>.</summary>
    public IReadOnlyList<string>? CustomClaimsAllowlist { get; init; }

    /// <summary>Extra parameters sent with every authorization request. Reserved OAuth parameters such as <c>state</c> are rejected.</summary>
    public IReadOnlyDictionary<string, string>? AuthorizationParameters { get; init; }

    /// <summary>Whether users can sign in with the provider. The server default is <c>true</c>.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Whether users without an email address can sign in. The server default is <c>false</c>.</summary>
    public bool? EmailOptional { get; init; }

    /// <summary>OIDC only: the discovery URL, when it differs from <c>{issuer}/.well-known/openid-configuration</c>.</summary>
    public Uri? DiscoveryUrl { get; init; }

    /// <summary>OIDC only: skips the nonce check. Not recommended in production. The server default is <c>false</c>.</summary>
    public bool? SkipNonceCheck { get; init; }

    /// <summary>OAuth 2.0 only: the JSON Web Key Set used to validate the provider's tokens.</summary>
    public Uri? JwksUri { get; init; }
}
