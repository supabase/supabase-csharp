using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// The SAML 2.0 configuration of a single sign-on identity provider.
/// </summary>
public sealed record SamlConfiguration
{
    /// <summary>The entity ID from the identity provider's metadata.</summary>
    public string EntityId { get; init; } = null!;

    /// <summary>
    /// The identity provider's metadata XML. Null on listed providers, which leave it out; fetch a single provider to
    /// read it.
    /// </summary>
    public string? MetadataXml { get; init; }

    /// <summary>The URL the metadata was fetched from. Null when the metadata was given as XML.</summary>
    public Uri? MetadataUrl { get; init; }

    /// <summary>
    /// How SAML assertion attributes map to user metadata, keyed by the user metadata key. Empty when the provider
    /// has no mapping.
    /// </summary>
    public IReadOnlyDictionary<string, SamlAttribute> AttributeMapping { get; init; } = new Dictionary<string, SamlAttribute>();

    /// <summary>The <c>NameID</c> format requested from the identity provider. Null when none is set.</summary>
    public SamlNameIdFormat? NameIdFormat { get; init; }
}
