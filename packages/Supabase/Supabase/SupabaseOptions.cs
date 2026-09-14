using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
    public IGotrueSessionPersistence<Session> SessionHandler { get; set; } = new DefaultSupabaseSessionHandler();

    /// <summary>
    /// Allows developer to specify options that will be passed to all child Supabase clients.
    /// </summary>
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
}
