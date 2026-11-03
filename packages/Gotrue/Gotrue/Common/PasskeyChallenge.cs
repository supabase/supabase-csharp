using System;
using System.Text.Json;

namespace Supabase.Gotrue.Common;

/// <summary>
/// A WebAuthn challenge issued by the auth server to register a passkey or to sign in with one. Hand
/// <see cref="Options" /> to the platform's WebAuthn API (for example <c>navigator.credentials.create()</c> or
/// <c>navigator.credentials.get()</c> through JavaScript interop, or Android's Credential Manager), then pass the
/// credential it produces and <see cref="Id" /> to
/// <see cref="Interfaces.IGotrueClient{TUser,TSession}.RegisterPasskeyAsync" /> or
/// <see cref="Interfaces.IGotrueClient{TUser,TSession}.SignInWithPasskeyAsync" />.
/// </summary>
public sealed record PasskeyChallenge
{
    /// <summary>The id of the challenge, which the registration or sign-in names. A challenge can be used once.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The WebAuthn options, in the W3C JSON format with binary fields as base64url strings, exactly as the server
    /// issued them: <c>PublicKeyCredentialCreationOptions</c> for a registration, which exclude the passkeys the user
    /// already registered, or <c>PublicKeyCredentialRequestOptions</c> for a sign-in, which allow any passkey the user
    /// registered (a discoverable sign-in).
    /// </summary>
    public JsonElement Options { get; init; }

    /// <summary>When the challenge stops being accepted.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
