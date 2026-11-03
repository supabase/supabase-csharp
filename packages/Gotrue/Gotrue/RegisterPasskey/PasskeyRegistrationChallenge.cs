using System;
using System.Text.Json;

namespace Supabase.Gotrue.RegisterPasskey;

/// <summary>
/// A passkey registration challenge issued by the auth server for the signed-in user. Hand <see cref="Options" /> to the
/// platform's WebAuthn API (for example <c>navigator.credentials.create()</c> through JavaScript interop, or Android's
/// Credential Manager), then pass the credential it creates and <see cref="Id" /> to
/// <see cref="Interfaces.IGotrueClient{TUser,TSession}.RegisterPasskeyAsync" />.
/// </summary>
public sealed record PasskeyRegistrationChallenge
{
    /// <summary>The id of the challenge, which the registration names. A challenge can be used once.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The W3C <c>PublicKeyCredentialCreationOptions</c>, in the WebAuthn JSON format with binary fields as base64url
    /// strings, exactly as the server issued them. They exclude the passkeys the user already registered.
    /// </summary>
    public JsonElement Options { get; init; }

    /// <summary>When the challenge stops being accepted.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
