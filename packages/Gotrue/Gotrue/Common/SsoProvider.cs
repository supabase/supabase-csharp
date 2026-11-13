using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// A single sign-on identity provider registered on the project. Users whose email domain is one of its
/// <see cref="Domains" /> sign in through it with SAML 2.0.
/// </summary>
public sealed record SsoProvider
{
    /// <summary>The server-assigned id, which <see cref="SsoProviderReference.ById" /> addresses.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// An identifier of your choosing, which <see cref="SsoProviderReference.ByResourceId" /> addresses. Null when the
    /// provider was registered without one.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>Whether users can sign in through the provider.</summary>
    public bool Enabled { get; init; }

    /// <summary>The provider's SAML 2.0 configuration.</summary>
    public SamlConfiguration Saml { get; init; } = null!;

    /// <summary>The email domains whose users sign in through the provider, empty when it has none.</summary>
    public IReadOnlyList<string> Domains { get; init; } = Array.Empty<string>();

    /// <summary>When the provider was registered.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the provider was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
