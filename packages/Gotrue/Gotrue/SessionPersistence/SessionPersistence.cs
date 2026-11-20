using Supabase.Gotrue.Interfaces;

namespace Supabase.Gotrue;

/// <summary>
/// Ready-made <see cref="IGotrueSessionPersistence{TSession}"/> choices.
/// </summary>
public static class SessionPersistence
{
    /// <summary>
    /// Persists nothing: the session exists only in memory, for the lifetime of the client instance. It is never
    /// saved, and a client starts signed out because there is nothing to restore. Pass it to
    /// <see cref="Client.SetPersistence"/> or <c>SupabaseOptions.SessionHandler</c>; it is what a client uses when no
    /// persistence is configured.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new Supabase.Client(url, key, new SupabaseOptions { SessionHandler = SessionPersistence.Disabled });
    /// await client.Auth.SignInWithPassword(email, password);
    /// // client.Auth.CurrentSession is set; a new client starts signed out.
    /// </code>
    /// </example>
    public static IGotrueSessionPersistence<Session> Disabled { get; } = new DisabledSessionPersistence();
}
