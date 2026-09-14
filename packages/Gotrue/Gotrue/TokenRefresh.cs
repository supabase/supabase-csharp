using System;
using System.Threading;
using Supabase.Gotrue.Interfaces;
using static Supabase.Gotrue.Constants.AuthState;

namespace Supabase.Gotrue;

/// <summary>
/// Manages the auto-refresh of the Gotrue Session.
/// </summary>
public class TokenRefresh
{
    private readonly Client _client;

    /// <summary>
    /// Minimum wait between refresh attempts.
    /// supabase-js polls on a fixed 30 second tick (AUTO_REFRESH_TICK_DURATION_MS) and picks a
    /// refresh up on the next one, so an attempt never follows another sooner than a tick.
    /// </summary>
    private static readonly TimeSpan AutoRefreshTickDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Guards the timer field, so a stop and a re-arm cannot interleave.
    /// </summary>
    private readonly object timerGate = new();

    /// <summary>
    /// Internal timer reference for token refresh
    /// <see>
    ///     <cref>AutoRefreshToken</cref>
    /// </see>
    /// </summary>
    private Timer? _refreshTimer;

    /// <summary>
    /// Set by Shutdown, so a tick or refresh still in flight cannot bring the timer back.
    /// </summary>
    private volatile bool stopped;

    /// <summary>
    /// Turn on debug logging for the TokenRefresh
    /// </summary>
    public bool Debug;

    /// <summary>
    /// Sets up the TokenRefresh class, bound to a specific client
    /// </summary>
    /// <param name="client"></param>
    public TokenRefresh(Client client) => this._client = client;
    /// <summary>
    /// Turns the auto-refresh timer on or off based on the current auth state
    /// </summary>
    /// <param name="sender">The Client and Session data</param>
    /// <param name="stateChanged"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void ManageAutoRefresh(IGotrueClient<User, Session> sender, Constants.AuthState stateChanged)
    {
        switch (stateChanged)
        {
            case SignedIn:
                if (this.Debug)
                    this._client.Debug("Refresh Timer started");
                this.CreateNewTimer();
                // Turn on auto-refresh timer
                break;
            case SignedOut:
                this.StopTimer();
                break;
            case Shutdown:
                this.stopped = true;
                this.StopTimer();
                break;
            case UserUpdated:
                if (this.Debug)
                    this._client.Debug("Refresh Timer restarted");
                this.CreateNewTimer();
                break;
            case TokenRefreshed:
                if (this.Debug)
                    this._client.Debug("Refresh Timer restarted");
                // A fresh token needs no refresh within a tick, so a zero expires_in cannot hot-loop.
                this.CreateNewTimer(AutoRefreshTickDuration);
                break;
            case PasswordRecovery:
            case MfaChallengeVerified:
                // Doesn't affect auto refresh
                break;
            default: throw new ArgumentOutOfRangeException(nameof(stateChanged), stateChanged, null);
        }
    }

    private void StopTimer()
    {
        lock (this.timerGate)
        {
            // A sign-in raced this SignedOut, so its timer stays.
            if (!this.stopped && this._client.CurrentSession != null)
                return;
            if (this.Debug)
                this._client.Debug("Refresh Timer stopped");
            this._refreshTimer?.Dispose();
            this._refreshTimer = null;
        }
    }

    /// <summary>
    /// The timer calls this method at the configured interval to refresh the token.
    ///
    /// If the user is offline, it won't try to refresh the token.
    /// </summary>
    private async void HandleRefreshTimerTick(object _)
    {
        var refreshCompleted = false;
        try
        {
            if (this._client.Online)
            {
                await this._client.RefreshToken();
                refreshCompleted = true;
            }
        }
        catch (Exception ex)
        {
            // Something unusually bad happened!
            if (this.Debug)
                this._client.Debug(ex.Message, ex);
        }
        finally
        {
            // A successful refresh is rescheduled by TokenRefreshed, a skipped or failed
            // one waits a tick instead of hot-looping on a session that schedules at zero.
            if (!refreshCompleted)
            {
                this.CreateNewTimer(AutoRefreshTickDuration);
            }
        }
    }

    /// <summary>
    /// Create a new refresh timer.
    /// 
    /// <para/>
    /// We pass <see cref="Timeout.InfiniteTimeSpan"/> to ensure the handler only runs once.
    /// We create a new timer after each refresh instead of a repeating one, so the next tick is
    /// scheduled from the session we ended up with rather than from a fixed interval.
    /// The callbacks run on the thread pool, so a timer per refresh is cheap at this cadence.
    /// </summary>
    private void CreateNewTimer(TimeSpan minimumDelay = default)
    {
        try
        {
            var refreshDueTime = this.GetSecondsUntilNextRefresh();
            if (refreshDueTime < minimumDelay)
                refreshDueTime = minimumDelay;
            lock (this.timerGate)
            {
                if (this.stopped || this._client.CurrentSession == null)
                {
                    if (this.Debug)
                        this._client.Debug($"No session, refresh timer not started");
                    return;
                }
                this._refreshTimer?.Dispose();
                this._refreshTimer = new Timer(this.HandleRefreshTimerTick, null, refreshDueTime, Timeout.InfiniteTimeSpan);
            }

            if (this.Debug)
                this._client.Debug($"Refresh timer scheduled {refreshDueTime.TotalMinutes} minutes");
        }
        catch (Exception e)
        {
            if (this.Debug)
                this._client.Debug($"Failed to initialize refresh timer", e);
        }
    }

    /// <summary>
    /// Returns remaining seconds until the access token should be refreshed.
    /// Interval is calculated as:<code>t - (1/5(n))</code> (i.e. if session time (t) 3600s, attempt refresh at 2880s or 720s (1/5) seconds before expiration).
    /// <remarks>
    /// - The maximum refresh wait time is clamped to <see cref="ClientOptions.MaximumRefreshWaitTime"/>
    /// </remarks>
    /// <remarks>
    /// - If the access token is expired it will refresh immediately.
    /// </remarks>
    /// </summary>
    /// <returns>The remaining seconds until the token should be refreshed</returns>
    private TimeSpan GetSecondsUntilNextRefresh()
    {
        if (this._client.CurrentSession is null || this._client.CurrentSession.AccessToken == null)
        {
            return TimeSpan.Zero;
        }

        var interval = (long) Math.Floor(this._client.CurrentSession.ExpiresIn * 4.0 / 5.0);
        var refreshAt = this._client.CurrentSession.CreatedAt.AddSeconds(interval);

        var secondsUntilNextRefresh = Convert.ToInt64((refreshAt - DateTime.UtcNow).TotalSeconds);

        if (secondsUntilNextRefresh < 0)
            return TimeSpan.Zero;

        if (secondsUntilNextRefresh > this._client.Options.MaximumRefreshWaitTime)
            secondsUntilNextRefresh = this._client.Options.MaximumRefreshWaitTime;

        return TimeSpan.FromSeconds(secondsUntilNextRefresh);
    }
}
