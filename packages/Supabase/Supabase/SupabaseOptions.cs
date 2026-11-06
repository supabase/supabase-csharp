using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Supabase.Core.Attributes;
using Supabase.Core.Http;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Realtime.Sockets;

namespace Supabase;

/// <summary>
/// Options available for Supabase Client Configuration
/// </summary>
public class SupabaseOptions
{
    /// <summary>
    /// Schema to be used in Postgres / Realtime
    /// </summary>
    public string Schema = "public";

    /// <summary>
    /// Should the Client automatically handle refreshing the User's Token?
    /// </summary>
    public bool AutoRefreshToken { get; set; } = true;

    /// <summary>
    /// Should the Client automatically connect to Realtime?
    /// </summary>
    public bool AutoConnectRealtime { get; set; }

    /// <summary>
    /// Functions passed to Gotrue that handle sessions.
    ///
    /// **By default these do nothing for persistence.**
    /// </summary>
    [ImplementedCapability("client.session_management.custom_storage")]
    [PartialCapability("client.session_management.persist_session", "No boolean persist toggle; in-memory-only behavior is achievable only by supplying a no-op IGotrueSessionPersistence handler.")]
    public IGotrueSessionPersistence<Session> SessionHandler { get; set; } = new DefaultSupabaseSessionHandler();

    /// <summary>
    /// Allows developer to specify options that will be passed to all child Supabase clients.
    /// </summary>
    [ImplementedCapability("client.request_configuration.global_headers")]
    public Dictionary<string, string> Headers = new();

    /// <summary>
    /// Specifies Options passed to the StorageClient.
    /// </summary>
    public Storage.ClientOptions StorageClientOptions { get; set; } = new();

    /// <summary>
    /// Retry policy passed to the Postgrest client. Defaults to no retries.
    /// </summary>
    public Supabase.Core.Http.RetryOptions PostgrestRetry { get; set; } = new();

    /// <summary>
    /// Timeout passed to the Postgrest client. Defaults to none.
    /// </summary>
    public TimeSpan? PostgrestTimeout { get; set; }

    /// <summary>
    /// Retry policy passed to the Auth client. Defaults to no retries.
    /// </summary>
    public Supabase.Core.Http.RetryOptions GotrueRetry { get; set; } = new();

    /// <summary>
    /// Retry policy passed to the Functions client. Defaults to no retries.
    /// </summary>
    public Supabase.Core.Http.RetryOptions FunctionsRetry { get; set; } = new();

    /// <summary>
    /// An HttpClient shared by the Auth, Postgrest, and Functions clients. Storage configures its own set
    /// of clients via <see cref="StorageClientOptions"/>; Realtime has no HTTP transport (see
    /// <see cref="WebSocketFactory"/>). When null, each client builds and owns its own.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// A proxy shared by the Auth, Postgrest, and Functions clients. Only used when <see cref="HttpClient"/>
    /// is not supplied.
    /// </summary>
    public IWebProxy? Proxy { get; set; }

    /// <summary>
    /// Builds the WebSocket transport the Realtime client connects through. Left null, the default
    /// <see cref="System.Net.WebSockets.ClientWebSocket"/>-backed transport is used.
    /// </summary>
    public IWebSocketFactory? WebSocketFactory { get; set; }

    /// <summary>
    /// The clock every child client's timers, delays and retry backoff run on: the Auth refresh timer, the
    /// Realtime heartbeat, push, rejoin and deferred-disconnect timers, and the backoff of
    /// <see cref="PostgrestRetry"/>, <see cref="GotrueRetry"/>, <see cref="FunctionsRetry"/> and
    /// <see cref="StorageClientOptions"/>' <c>Retry</c>. Supply one to drive them all from a host's own
    /// timers (or a fake clock in tests). Left null, every child keeps its own default,
    /// <see cref="System.TimeProvider.System"/>.
    /// </summary>
    /// <remarks>
    /// The retry policies are copied with the clock applied, so the <see cref="RetryOptions"/> instances set
    /// here are left as they are. Storage's options object is the caller's own and is passed through, so
    /// its <c>Retry</c> is updated in place.
    /// </remarks>
    /// <example>
    /// <code>
    /// var client = new Supabase.Client(url, key, new SupabaseOptions { TimeProvider = myMainThreadClock });
    /// </code>
    /// </example>
    public TimeProvider? TimeProvider { get; set; }

    /// <summary>
    /// The Supabase Auth Url Format
    /// </summary>
    public string AuthUrlFormat { get; set; } = "{0}/auth/v1";

    /// <summary>
    /// The Supabase Postgrest Url Format
    /// </summary>
    public string RestUrlFormat { get; set; } = "{0}/rest/v1";

    /// <summary>
    /// The Supabase Realtime Url Format
    /// </summary>
    public string RealtimeUrlFormat { get; set; } = "{0}/realtime/v1";

    /// <summary>
    /// The Supabase Storage Url Format
    /// </summary>
    public string StorageUrlFormat { get; set; } = "{0}/storage/v1";

    /// <summary>
    /// The Supabase Functions Url Format
    /// </summary>
    public string FunctionsUrlFormat { get; set; } = "{0}/functions/v1";

    /// <summary>
    /// The clock handed to the Auth and Realtime clients: <see cref="TimeProvider"/>, or their default.
    /// </summary>
    internal TimeProvider ChildTimeProvider => this.TimeProvider ?? System.TimeProvider.System;

    /// <summary>
    /// Returns <paramref name="retry"/> waiting on <see cref="TimeProvider"/>, or unchanged when none is set.
    /// </summary>
    internal RetryOptions WithTimeProvider(RetryOptions retry) =>
        this.TimeProvider is { } clock ? retry with { TimeProvider = clock } : retry;

    /// <summary>
    /// Returns <see cref="StorageClientOptions"/>, its <c>Retry</c> waiting on <see cref="TimeProvider"/> when one is set.
    /// </summary>
    internal Storage.ClientOptions StorageClientOptionsWithTimeProvider()
    {
        this.StorageClientOptions.Retry = this.WithTimeProvider(this.StorageClientOptions.Retry);
        return this.StorageClientOptions;
    }
}
