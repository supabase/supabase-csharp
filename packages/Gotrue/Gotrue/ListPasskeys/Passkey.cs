using System;

namespace Supabase.Gotrue.ListPasskeys;

/// <summary>
/// A passkey (WebAuthn credential) registered by the signed-in user. Only its metadata is returned: the credential's
/// key material never leaves the auth server.
/// </summary>
public sealed record Passkey
{
    /// <summary>The server-assigned id of the passkey.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The human-readable name of the passkey, which the server derives from the authenticator that created it
    /// (for example <c>iCloud Keychain</c>), or null when it has none.
    /// </summary>
    public string? FriendlyName { get; init; }

    /// <summary>When the passkey was registered.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the passkey last signed the user in, or null when it never has.</summary>
    public DateTime? LastUsedAt { get; init; }
}
