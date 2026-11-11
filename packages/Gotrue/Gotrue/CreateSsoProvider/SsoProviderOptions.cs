using System.Collections.Generic;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.CreateSsoProvider;

/// <summary>
/// Optional settings of a single sign-on identity provider to register. The auth server applies its default to each
/// one left null.
/// </summary>
public sealed record SsoProviderOptions
{
    /// <summary>
    /// The email domains whose users sign in through the provider. A domain can belong to one provider only. Null
    /// registers none.
    /// </summary>
    public IReadOnlyList<string>? Domains { get; init; }

    /// <summary>
    /// How SAML assertion attributes map to user metadata, keyed by the user metadata key. Null maps none.
    /// </summary>
    public IReadOnlyDictionary<string, SamlAttribute>? AttributeMapping { get; init; }

    /// <summary>The <c>NameID</c> format to request from the identity provider. Null requests none.</summary>
    public SamlNameIdFormat? NameIdFormat { get; init; }

    /// <summary>
    /// An identifier of your choosing, unique across the project, to address the provider by with
    /// <see cref="SsoProviderReference.ByResourceId" />. A resource ID containing <c>/</c> is accepted, but such a
    /// provider can only be listed or addressed by its id, never by its resource ID. Null registers none.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>Whether users can sign in through the provider. Null leaves it enabled.</summary>
    public bool? Enabled { get; init; }
}
