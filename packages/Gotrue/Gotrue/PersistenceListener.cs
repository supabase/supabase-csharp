using System;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Gotrue.Interfaces;
namespace Supabase.Gotrue;

/// <summary>
/// Manages the persistence of the Gotrue Session. You'll want to install a persistence listener
/// to persist user sessions between app restarts. 
/// </summary>
public class PersistenceListener : IGotruePersistenceListener<Session>
{
    /// <summary>
    /// Create a new persistence listener
    /// </summary>
    /// <param name="persistence"></param>
    public PersistenceListener(IGotrueSessionPersistence<Session> persistence) => this.Persistence = persistence;

    /// <inheritdoc />
    public IGotrueSessionPersistence<Session> Persistence { get; }

    /// <summary>
    /// If you install a persistence listener, it will be called when the user signs in and signs out.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="stateChanged"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void EventHandler(IGotrueClient<User, Session> sender, Constants.AuthState stateChanged)
    {
        switch (stateChanged)
        {
            case Constants.AuthState.SignedIn:
            case Constants.AuthState.MfaChallengeVerified:
                if (sender == null)
                    throw new ArgumentException("Tried to save a null session (1)");
                if (sender.CurrentSession == null)
                    throw new ArgumentException("Tried to save a null session (2)");

                this.Persistence.SaveSession(sender.CurrentSession);
                break;
            case Constants.AuthState.SignedOut:
                this.Persistence.DestroySession();
                break;
            case Constants.AuthState.UserUpdated:
                if (sender == null)
                    throw new ArgumentException("Tried to save a null session (1)");
                if (sender.CurrentSession == null)
                    throw new ArgumentException("Tried to save a null session (2)");

                this.Persistence.SaveSession(sender.CurrentSession);
                break;
            case Constants.AuthState.PasswordRecovery: break;
            case Constants.AuthState.TokenRefreshed:
                if (sender.CurrentSession != null)
                {
                    this.Persistence.SaveSession(sender.CurrentSession);
                }
                break;
            case Constants.AuthState.Shutdown:
                // The session should have already been saved, so we don't need to do anything here.
                break;
            default: throw new ArgumentOutOfRangeException(nameof(stateChanged), stateChanged, null);
        }
    }

    /// <summary>
    /// The awaited counterpart to <see cref="EventHandler" />, routing state changes to the persistence
    /// implementation's async members.
    /// </summary>
    public async Task EventHandlerAsync(IGotrueClient<User, Session> sender, Constants.AuthState stateChanged, CancellationToken cancellationToken = default)
    {
        switch (stateChanged)
        {
            case Constants.AuthState.SignedIn:
            case Constants.AuthState.MfaChallengeVerified:
            case Constants.AuthState.UserUpdated:
                if (sender == null)
                    throw new ArgumentException("Tried to save a null session (1)");
                if (sender.CurrentSession == null)
                    throw new ArgumentException("Tried to save a null session (2)");

                await this.Persistence.SaveSessionAsync(sender.CurrentSession, cancellationToken).ConfigureAwait(false);
                break;
            case Constants.AuthState.SignedOut:
                await this.Persistence.DestroySessionAsync(cancellationToken).ConfigureAwait(false);
                break;
            case Constants.AuthState.PasswordRecovery: break;
            case Constants.AuthState.TokenRefreshed:
                if (sender.CurrentSession != null)
                {
                    await this.Persistence.SaveSessionAsync(sender.CurrentSession, cancellationToken).ConfigureAwait(false);
                }
                break;
            case Constants.AuthState.Shutdown:
                break;
            default: throw new ArgumentOutOfRangeException(nameof(stateChanged), stateChanged, null);
        }
    }
}
