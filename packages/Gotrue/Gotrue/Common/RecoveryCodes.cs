using System;
using System.Collections.Generic;

namespace Supabase.Gotrue.Common;

/// <summary>
/// A freshly generated set of the signed-in user's single-use MFA recovery codes. The codes are returned exactly once
/// and cannot be read back from the server: show them to the user and ask them to store them safely.
/// </summary>
public sealed record RecoveryCodes
{
    /// <summary>The id of the user's recovery codes factor, as listed in <see cref="User.Factors" />.</summary>
    public string FactorId { get; init; } = null!;

    /// <summary>The name of the recovery codes factor shown in the user's factor list.</summary>
    public string FriendlyName { get; init; } = null!;

    /// <summary>
    /// The plaintext codes in canonical form: lowercase, without separators. Group them for display if you like; the
    /// server ignores letter case, whitespace and <c>-</c> when a code is verified.
    /// </summary>
    public IReadOnlyList<string> Codes { get; init; } = Array.Empty<string>();
}
