using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// How one user metadata value is read from a SAML assertion.
/// </summary>
public sealed record SamlAttribute
{
    /// <summary>
    /// The assertion attributes to read, tried in order: the first one that carries a value wins.
    /// </summary>
    public IReadOnlyList<string> Names { get; init; } = Array.Empty<string>();

    /// <summary>The value used when the assertion carries none of the <see cref="Names" />. Null for none.</summary>
    public object? Default { get; init; }

    /// <summary>
    /// Whether every value of a multi-valued attribute is kept, as a list, instead of only the first one.
    /// </summary>
    public bool KeepsAllValues { get; init; }
}
