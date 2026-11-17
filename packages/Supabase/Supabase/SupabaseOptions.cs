using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
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
    /// The OAuth flow type <see cref="Client.Auth"/> uses when a call's own options leave it unset. Defaults to
    /// <see cref="Constants.OAuthFlowType.Implicit"/>; PKCE is recommended for mobile and server-side applications.
    /// A flow type set on a call's options (such as <see cref="SignInOptions.FlowType"/>) wins over it. Passed to
    /// Auth as <see cref="Gotrue.ClientOptions.FlowType"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new Supabase.Client(url, key, new SupabaseOptions { AuthFlowType = OAuthFlowType.PKCE });
    /// var state = await client.Auth.SignIn(Provider.Github);
    /// var session = await client.Auth.ExchangeCodeForSession(state.PKCEVerifier!, codeFromCallback);
    /// </code>
    /// </example>
    [ImplementedCapability("client.authentication_integration.oauth_flow_type")]
    public Constants.OAuthFlowType AuthFlowType { get; set; } = Constants.OAuthFlowType.Implicit;

    /// <summary>
    /// Should the Client automatically connect to Realtime?
    /// </summary>
    public bool AutoConnectRealtime { get; set; }

    /// <summary>
    /// Where Gotrue persists the session. Defaults to <see cref="SessionPersistence.Disabled"/>: nothing is
    /// persisted and the session lives only in memory for the lifetime of the client instance. Supply your own
    /// <see cref="IGotrueSessionPersistence{TSession}"/> to persist it.
    /// </summary>
    [ImplementedCapability("client.session_management.custom_storage")]
    [ImplementedCapability("client.session_management.persist_session")]
    public IGotrueSessionPersistence<Session> SessionHandler { get; set; } = SessionPersistence.Disabled;

    /// <summary>
    /// Authenticates the client through a third-party auth provider (e.g. Clerk, Auth0, Firebase, Cognito) instead
    /// of Supabase Auth. The callback returns the provider's current JWT, or null when no user is signed in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is awaited before every Database, Storage and Edge Functions request, and the token it returns is sent as
    /// <c>Authorization: Bearer</c>. Realtime asks it for the token on connect and on every heartbeat. A null token
    /// sends the request with the project API key, as when no user is signed in. A callback that throws fails the
    /// request that called it with the same exception; the client never falls back to the API key.
    /// </para>
    /// <para>
    /// The client then builds no Supabase Auth client: <see cref="Client.Auth"/> and <see cref="Client.AdminAuth"/>
    /// throw <see cref="InvalidOperationException"/>, so resolving the Auth client from dependency injection throws
    /// too. <see cref="Client.InitializeAsync"/> restores no session and only connects Realtime when
    /// <see cref="AutoConnectRealtime"/> is set. <see cref="SessionHandler"/>, <see cref="AutoRefreshToken"/> and
    /// <see cref="AuthFlowType"/> are ignored. The project must be configured to trust the provider's JWTs.
    /// </para>
    /// <para>
    /// Only the <see cref="Client(string, string?, SupabaseOptions?)"/> constructor reads it: the constructor that
    /// takes ready-made sub-clients and <see cref="StatelessClient"/> ignore it. It cannot be combined with an
    /// <c>Authorization</c> entry in <see cref="Headers"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var client = new Supabase.Client(url, publishableKey, new SupabaseOptions
    /// {
    ///     ThirdPartyAccessToken = cancellationToken => clerk.GetTokenAsync(cancellationToken)
    /// });
    /// var todos = await client.From&lt;Todo&gt;().Get();
    /// </code>
    /// </example>
    [ImplementedCapability("client.authentication_integration.third_party_auth")]
    public Func<CancellationToken, Task<string?>>? ThirdPartyAccessToken { get; set; }

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
    /// The HttpClient every Database, Auth, Storage and Edge Functions request is sent through. The SDK sets the
    /// <c>apikey</c> and <c>Authorization</c> headers on each request and never changes the client, so one client can
    /// serve several Supabase clients. When null, each sub-client builds and owns its own. Realtime has no HTTP
    /// transport here (see <see cref="WebSocketFactory"/>).
    /// </summary>
    /// <remarks>
    /// Storage uses this client for requests, uploads and downloads alike, unless
    /// <see cref="StorageClientOptions"/> names a client for one of them. The client's own
    /// <see cref="System.Net.Http.HttpClient.Timeout"/> then governs uploads and downloads, so set it to
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> when you transfer large files.
    /// </remarks>
    /// <example>
    /// <code>
    /// var http = new HttpClient(new MyLoggingHandler());
    /// var client = new Supabase.Client(url, key, new SupabaseOptions { HttpClient = http });
    /// </code>
    /// </example>
    [ImplementedCapability("client.request_configuration.custom_http_client")]
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
    /// Whether the SDK adds the current trace context (<c>traceparent</c>, <c>tracestate</c>, <c>baggage</c>) to each request it
    /// sends. Defaults to <see cref="Supabase.Core.Http.TracePropagation.Enabled"/>; <see cref="Supabase.Core.Http.TracePropagation.Disabled"/> stops the SDK from
    /// injecting it, and the .NET runtime's own injection is controlled through
    /// <see cref="System.Diagnostics.DistributedContextPropagator.Current"/>. Passed to the Auth, Database, Storage and Edge
    /// Functions clients; Realtime does not propagate trace context.
    /// </summary>
    /// <example>
    /// <code>
    /// using var activity = source.StartActivity("checkout");
    /// await client.From&lt;Order&gt;().Get();
    /// </code>
    /// </example>
    [ImplementedCapability("client.observability.trace_propagation")]
    public Supabase.Core.Http.TracePropagation TracePropagation { get; set; } = Supabase.Core.Http.TracePropagation.Enabled;

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
