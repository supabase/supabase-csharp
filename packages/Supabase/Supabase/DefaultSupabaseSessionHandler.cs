using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Supabase;

/// <summary>
/// Represents the default session handler for Gotrue - it does nothing by default.
/// </summary>
[System.Obsolete("Use SessionPersistence.Disabled. DefaultSupabaseSessionHandler is removed in 9.0.0.")]
public class DefaultSupabaseSessionHandler : IGotrueSessionPersistence<Session>
{
    /// <summary>
    /// Default Session Save (does nothing by default)
    /// </summary>
    /// <param name="session"></param>
    public void SaveSession(Session session)
    {
    }

    /// <summary>
    /// Default Session Destroyer (does nothing by default)
    /// </summary>
    public void DestroySession()
    {
    }

    /// <summary>
    /// Default Session Loader (does nothing by default)
    /// </summary>
    /// <returns></returns>
    public Session? LoadSession() => null;
}