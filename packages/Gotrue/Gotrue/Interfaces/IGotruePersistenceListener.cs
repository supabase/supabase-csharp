using System.Threading;
using System.Threading.Tasks;

namespace Supabase.Gotrue.Interfaces;

/// <summary>
/// Interface for a session persistence auth state handler.
/// </summary>
public interface IGotruePersistenceListener<TSession> where TSession : Session
{
    /// <summary>
    /// The persistence implementation for the client (e.g. file system, local storage, etc).
    /// </summary>
    IGotrueSessionPersistence<TSession> Persistence { get; }

    /// <summary>
    /// Routes auth state changes to the persistence implementation.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="stateChanged"></param>
    void EventHandler(IGotrueClient<User, TSession> sender, Constants.AuthState stateChanged);

    /// <summary>
    /// Routes auth state changes to the persistence implementation asynchronously, so an async-only
    /// store (e.g. Blazor WASM local storage) can be awaited instead of blocked.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="stateChanged"></param>
    /// <param name="cancellationToken"></param>
    Task EventHandlerAsync(IGotrueClient<User, TSession> sender, Constants.AuthState stateChanged, CancellationToken cancellationToken = default);
}
