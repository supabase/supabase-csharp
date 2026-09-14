using System.Threading;
using System.Threading.Tasks;

namespace Supabase.Gotrue.Interfaces;

/// <summary>
/// Interface for session persistence. As a reminder, make sure you handle exceptions and
/// other error conditions in your implementation.
/// </summary>
public interface IGotrueSessionPersistence<TSession>
    where TSession : Session
{
    /// <summary>
    /// Saves the session to the persistence implementation.
    /// </summary>
    /// <param name="session"></param>
    void SaveSession(TSession session);

    /// <summary>
    /// Destroys the session in the persistence implementation. Usually this means
    /// deleting the session file or clearing local storage.
    /// </summary>
    void DestroySession();

    /// <summary>
    /// Loads the session from the persistence implementation. Returns null if there is no session.
    /// </summary>
    /// <returns></returns>
    TSession? LoadSession();

    /// <summary>
    /// Saves the session to the persistence implementation asynchronously.
    /// The default implementation calls the synchronous <see cref="SaveSession" />; async-only
    /// stores (e.g. Blazor WASM local storage over JS interop) should override this instead.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="cancellationToken"></param>
    Task SaveSessionAsync(TSession session, CancellationToken cancellationToken = default)
    {
        this.SaveSession(session);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Destroys the session in the persistence implementation asynchronously.
    /// The default implementation calls the synchronous <see cref="DestroySession" />; async-only
    /// stores should override this instead.
    /// </summary>
    /// <param name="cancellationToken"></param>
    Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        this.DestroySession();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads the session from the persistence implementation asynchronously. Returns null if there is no session.
    /// The default implementation calls the synchronous <see cref="LoadSession" />; async-only
    /// stores should override this instead.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TSession?> LoadSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(this.LoadSession());
}
