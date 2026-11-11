using System;

namespace Supabase.Gotrue.Common;

/// <summary>
/// Where the auth server reads a SAML 2.0 identity provider's metadata from: a URL it fetches, with
/// <see cref="FromUrl" />, or the XML itself, with <see cref="FromXml" />.
/// </summary>
public sealed record SamlMetadata
{
    private SamlMetadata(Uri? url, string? xml)
    {
        this.Url = url;
        this.Xml = xml;
    }

    internal Uri? Url { get; }

    internal string? Xml { get; }

    /// <summary>
    /// Metadata the auth server fetches from <paramref name="url" /> before it answers. The server only accepts an
    /// HTTPS URL it can reach.
    /// </summary>
    /// <param name="url">The absolute URL of the identity provider's metadata.</param>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="url" /> is relative.</exception>
    public static SamlMetadata FromUrl(Uri url) => new(EnsureAbsolute(url), null);

    /// <summary>The identity provider's metadata XML, as the identity provider publishes it.</summary>
    /// <param name="xml">The metadata XML.</param>
    /// <exception cref="ArgumentNullException"><paramref name="xml" /> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="xml" /> is empty or whitespace.</exception>
    public static SamlMetadata FromXml(string xml) => new(null, EnsureNotBlank(xml));

    private static Uri EnsureAbsolute(Uri url)
    {
        if (url is null)
            throw new ArgumentNullException(nameof(url));
        if (!url.IsAbsoluteUri)
            throw new ArgumentException("The metadata URL must be absolute.", nameof(url));
        return url;
    }

    private static string EnsureNotBlank(string xml)
    {
        if (xml is null)
            throw new ArgumentNullException(nameof(xml));
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("The metadata XML cannot be empty.", nameof(xml));
        return xml;
    }
}
