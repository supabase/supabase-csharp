using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Supabase.Core.Http;
using static Supabase.Gotrue.Constants;

namespace Supabase.Gotrue;

/// <summary>
/// Class representation options available to the <see cref="Client"/>.
/// </summary>
public class ClientOptions
{
    /// <summary>
    /// Gotrue Endpoint
    /// </summary>
    public string Url { get; set; } = GOTRUE_URL;

    /// <summary>
    /// Headers to be sent with subsequent requests.
    /// </summary>
    public Dictionary<string, string> Headers = new Dictionary<string, string>();

    /// <summary>
    /// An HttpClient to send requests through. When null, the client builds and owns its own.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// A proxy to route requests through. Only used when <see cref="HttpClient"/> is not supplied.
    /// </summary>
    public IWebProxy? Proxy { get; set; }

    /// <summary>
    /// Retry policy applied to each request. Default (<see cref="RetryOptions.MaxRetries"/> 0) sends once, unretried.
    /// </summary>
    public RetryOptions Retry { get; set; } = new RetryOptions();

    /// <summary>
    /// Should the Client automatically handle refreshing the User's Token?
    /// </summary>
    public bool AutoRefreshToken { get; set; } = true;

    /// <summary>
    /// Ask the TokenRefresh system to log extra debug info
    /// </summary>
    public bool DebugRefreshToken { get; set; } = false;

    /// <summary>
    /// By default, the Client will attempt to refresh the token when roughly 1/5 of the
    /// time is left before expiration (assuming AutoRefreshToken is true).
    ///
    /// <see cref="TokenRefresh.InitRefreshTimer"/>
    ///
    /// The default expiration time for GoTrue servers is 3600 (1 hour), with a maximum
    /// of 604,800 seconds (one week).
    ///
    /// If you set the expiration to one week, you may want to refresh the token a bit
    /// more frequently. This setting allows you to set a custom threshold for when the
    /// client should AutoRefreshToken. The default value is 14400 seconds (4 hours).
    ///
    /// In this scenario, if you set the server expiration to one week and leave this
    /// value set to the default, as long as the user logs in at least once a week they
    /// should stay logged in indefinitely.
    /// </summary>
    public int MaximumRefreshWaitTime { get; set; } = 14400;

    /// <summary>
    /// Allows unconfirmed users to be treated as signed in. Defaults to false.
    /// Sign-in returns null for an unconfirmed user unless this is on. During email or phone sign-up,
    /// enabling this adopts the returned user in a session without tokens and raises
    /// <see cref="Constants.AuthState.SignedIn" /> when confirmation is pending.
    /// </summary>
    public bool AllowUnconfirmedUserSessions { get; set; }
}
