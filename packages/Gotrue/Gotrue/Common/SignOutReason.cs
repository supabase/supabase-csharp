namespace Supabase.Gotrue.Common;

/// <summary>
///     Why the user was signed out, read from <see cref="Interfaces.IGotrueClient{TUser,TSession}.SignOutReason" />
///     while handling a <see cref="Constants.AuthState.SignedOut" /> state change.
/// </summary>
public enum SignOutReason
{
    /// <summary>The application called <see cref="Interfaces.IGotrueClient{TUser,TSession}.SignOut" />.</summary>
    UserInitiated,

    /// <summary>The server rejected the refresh token, so the session was removed without the user asking.</summary>
    SessionExpired,

    /// <summary>The stored session could not be recovered because the store no longer held it.</summary>
    SessionMissing,
}
