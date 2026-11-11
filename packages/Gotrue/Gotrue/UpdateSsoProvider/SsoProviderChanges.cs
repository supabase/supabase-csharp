using System;
using System.Collections.Generic;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.UpdateSsoProvider;

/// <summary>
/// Changes to a single sign-on identity provider. Every setting left null keeps its stored value, while an empty one
/// clears it. The <c>NameID</c> format is the exception: the auth server clears a stored format the request leaves
/// out, so every change states it, with <see cref="KeepingNameIdFormatOf" />, <see cref="WithNameIdFormat" /> or
/// <see cref="WithoutNameIdFormat" />.
/// </summary>
/// <remarks>
/// The auth server only stores a new <c>NameID</c> format together with a change to <see cref="Metadata" /> or
/// <see cref="AttributeMapping" />: a change of the format alone is answered as a success but not saved, until the
/// server is fixed.
/// </remarks>
public sealed record SsoProviderChanges
{
    private SsoProviderChanges(SamlNameIdFormat? nameIdFormat) => this.NameIdFormat = nameIdFormat;

    /// <summary>The <c>NameID</c> format the provider has after the change. Null when it has none.</summary>
    public SamlNameIdFormat? NameIdFormat { get; }

    /// <summary>
    /// New metadata for the provider, given <see cref="SamlMetadata.FromUrl" /> or <see cref="SamlMetadata.FromXml" />.
    /// It must carry the provider's entity ID. Null keeps the stored metadata.
    /// </summary>
    public SamlMetadata? Metadata { get; init; }

    /// <summary>
    /// The email domains whose users sign in through the provider, replacing the stored ones. Empty removes every
    /// domain; null keeps the stored ones.
    /// </summary>
    public IReadOnlyList<string>? Domains { get; init; }

    /// <summary>
    /// How SAML assertion attributes map to user metadata, keyed by the user metadata key, replacing the stored
    /// mapping. Empty clears the mapping; null keeps the stored one.
    /// </summary>
    public IReadOnlyDictionary<string, SamlAttribute>? AttributeMapping { get; init; }

    /// <summary>
    /// The identifier <see cref="SsoProviderReference.ByResourceId" /> addresses the provider by. Empty removes it;
    /// null keeps the stored one.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>Whether users can sign in through the provider. Null keeps the stored setting.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Changes that keep the <c>NameID</c> format <paramref name="current" /> has.</summary>
    /// <param name="current">The provider as last read, for example with <c>GetSsoProviderAsync</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="current" /> is null.</exception>
    public static SsoProviderChanges KeepingNameIdFormatOf(SsoProvider current) =>
        new((current ?? throw new ArgumentNullException(nameof(current))).Saml.NameIdFormat);

    /// <summary>Changes that set the <c>NameID</c> format to request from the identity provider.</summary>
    /// <param name="format">The format to request.</param>
    public static SsoProviderChanges WithNameIdFormat(SamlNameIdFormat format) => new(format);

    /// <summary>Changes that remove the <c>NameID</c> format, leaving it to the identity provider.</summary>
    public static SsoProviderChanges WithoutNameIdFormat() => new(nameIdFormat: null);
}
