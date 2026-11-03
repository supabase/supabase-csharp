using System;
using System.Text.Json;

namespace Supabase.Gotrue.SignInWithPasskey;

/// <summary>
/// A passkey sign-in challenge issued by the auth server. Hand <see cref="Options" /> to the platform's WebAuthn API
/// (for example <c>navigator.credentials.get()</c> through JavaScript interop, or Android's Credential Manager), then
/// pass the assertion it produces and <see cref="Id" /> to
/// <see cref="Interfaces.IGotrueClient{TUser,TSession}.SignInWithPasskeyAsync" />.
/// </summary>
public sealed record PasskeySignInChallenge
{
    /// <summary>The id of the challenge, which the sign-in names. A challenge can be used once.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The W3C <c>PublicKeyCredentialRequestOptions</c>, in the WebAuthn JSON format with binary fields as base64url
    /// strings, exactly as the server issued them. They allow any passkey the user registered (a discoverable sign-in).
    /// </summary>
    public JsonElement Options { get; init; }

    /// <summary>When the challenge stops being accepted.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
