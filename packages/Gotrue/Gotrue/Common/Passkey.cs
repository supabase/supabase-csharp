using System;

namespace Supabase.Gotrue.Common;

/// <summary>
/// A passkey (WebAuthn credential) registered by the signed-in user. Only its metadata is returned: the credential's
/// key material never leaves the auth server.
/// </summary>
public sealed record Passkey
{
    /// <summary>The server-assigned id of the passkey.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The human-readable name of the passkey, or null when it has none. The server derives it from the authenticator
    /// that created the passkey (for example <c>iCloud Keychain</c>) until the user renames it.
    /// </summary>
    public string? FriendlyName { get; init; }

    /// <summary>When the passkey was registered.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the passkey last signed the user in, or null when it never has.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
