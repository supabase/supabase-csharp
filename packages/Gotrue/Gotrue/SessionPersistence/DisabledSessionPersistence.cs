using Supabase.Gotrue.Interfaces;

namespace Supabase.Gotrue;

internal sealed class DisabledSessionPersistence : IGotrueSessionPersistence<Session>
{
    public void SaveSession(Session session)
    {
    }

    public void DestroySession()
    {
    }

    public Session? LoadSession() => null;
}
