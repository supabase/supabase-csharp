using System;
using System.Collections.Generic;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.UpdateOAuthClient;

/// <summary>
/// Changes to an OAuth client. A member left null keeps its current value, so a setting cannot be cleared: the
/// server reads an absent member as unchanged. The client type cannot change.
/// </summary>
public sealed record OAuthClientChanges
{
    /// <summary>The new human-readable name of the client.</summary>
    public string? Name { get; init; }

    /// <summary>The URIs the server may redirect to after an authorization, replacing the current ones: at least one, at most ten.</summary>
    public IReadOnlyList<Uri>? RedirectUris { get; init; }

    /// <summary>
    /// The new token endpoint authentication method. It must suit the client's type: <see cref="OAuthClientTokenEndpointAuthMethod.None" />
    /// for a public client, a client secret method for a confidential one.
    /// </summary>
    public OAuthClientTokenEndpointAuthMethod? TokenEndpointAuthMethod { get; init; }

    /// <summary>The grant types and URIs to change.</summary>
    public OAuthClientOptions? Options { get; init; }

    internal bool IsEmpty =>
        this.Name is null
        && this.RedirectUris is null
        && this.TokenEndpointAuthMethod is null
        && !this.SetsAnyOption;

    private bool SetsAnyOption =>
        this.Options is { } options && (options.GrantTypes is not null || options.ClientUri is not null || options.LogoUri is not null);
}
