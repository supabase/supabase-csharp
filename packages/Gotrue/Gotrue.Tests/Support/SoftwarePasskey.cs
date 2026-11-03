#region

using System;
using System.Security.Cryptography;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     A passkey held by <see cref="SoftwareAuthenticator" />: the credential id and P-256 private key it signs
///     assertions with, and the user handle the relying party bound to it at registration.
/// </summary>
/// <param name="Id">The server-assigned id of the passkey, or <see cref="Guid.Empty" /> when it was never registered.</param>
/// <param name="CredentialId">The WebAuthn credential id.</param>
/// <param name="Key">The credential's private key.</param>
/// <param name="UserHandle">The user handle from the registration options, which a discoverable sign-in returns.</param>
internal sealed record SoftwarePasskey(Guid Id, byte[] CredentialId, ECParameters Key, byte[] UserHandle);
