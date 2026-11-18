using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// An OAuth client registered with the project's OAuth 2.1 server. It never carries the client secret: the
/// server returns that only when the client is created or its secret is regenerated, as <see cref="OAuthClientCredentials" />.
/// </summary>
public sealed record OAuthClient
{
    /// <summary>The id the client identifies itself with. The server assigns it when the client is registered.</summary>
    public Guid ClientId { get; init; }

    /// <summary>The human-readable name of the client, or null when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>Whether the client can keep a secret.</summary>
    public OAuthClientType Type { get; init; }

    /// <summary>How the client authenticates to the token endpoint.</summary>
    public OAuthClientTokenEndpointAuthMethod TokenEndpointAuthMethod { get; init; }

    /// <summary>How the client was registered.</summary>
    public OAuthClientRegistrationType RegistrationType { get; init; }

    /// <summary>The URIs the server may redirect to after an authorization.</summary>
    public IReadOnlyList<Uri> RedirectUris { get; init; } = Array.Empty<Uri>();

    /// <summary>The grants the client may use at the token endpoint.</summary>
    public IReadOnlyList<OAuthClientGrantType> GrantTypes { get; init; } = Array.Empty<OAuthClientGrantType>();

    /// <summary>The home page of the client application, or null when it has none.</summary>
    public Uri? ClientUri { get; init; }

    /// <summary>The logo of the client application, or null when it has none.</summary>
    public Uri? LogoUri { get; init; }

    /// <summary>When the client was registered.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the client was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
