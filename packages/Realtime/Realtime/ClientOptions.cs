using System;
using System.Collections.Generic;
using System.Globalization;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime.Socket;
using Supabase.Realtime.Sockets;

namespace Supabase.Realtime;

/// <summary>
/// Options used when initializing a <see cref="Client"/>
/// </summary>
public class ClientOptions
{
    /// <summary>
    /// The function to encode outgoing messages. Defaults to JSON
    /// </summary>
    public Action<object, Action<string>>? Encode { get; set; }

    /// <summary>
    /// The function to decode incoming messages.
    /// </summary>
    public Action<string, Action<SocketResponse?>>? Decode { get; set; }

    /// <summary>
    /// The Websocket Transport, for example WebSocket.
    /// </summary>
    public string Transport { get; set; } = Constants.TransportWebsocket;

    /// <summary>
    /// The default timeout in milliseconds to trigger push timeouts.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(Constants.DefaultTimeout);

    /// <summary>
    /// @todo Presently unused: Limit the number of events that can be sent per second.
    /// </summary>
    public int EventsPerSecond { get; set; } = 10;

    /// <summary>
    /// The interval to send a heartbeat message
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The interval to reconnect
    /// </summary>
    public Func<int, TimeSpan> ReconnectAfterInterval { get; set; } = (tries) =>
    {
        var intervals = new[] { 30, 45, 60, 120 };
        return TimeSpan.FromSeconds(tries < intervals.Length ? intervals[tries] : intervals[intervals.Length - 1]);
    };

    /// <summary>
    /// Request headers to be appended to the connection string.
    /// </summary>
    public readonly Dictionary<string, string> Headers = new();

    /// <summary>
    /// The optional params to pass when connecting
    /// </summary>
    public SocketOptionsParameters Parameters = new();

    /// <summary>
    /// Datetime Style for JSON Deserialization of Models
    /// </summary>
    public readonly DateTimeStyles DateTimeStyles = DateTimeStyles.AdjustToUniversal;

    /// <summary>
    /// Datetime format for JSON Deserialization of Models (Postgrest style)
    /// </summary>
    public string DateTimeFormat { get; set; } = @"yyyy'-'MM'-'dd' 'HH':'mm':'ss.FFFFFFK";

    /// <summary>
    /// Optional Postgrest client. When set, every model returned by
    /// <see cref="Supabase.Realtime.PostgresChanges.PostgresChangesResponse.Model{TModel}"/> and
    /// <c>OldModel&lt;TModel&gt;</c> has this client's context (BaseUrl, RequestClientOptions, headers) attached to
    /// it via <c>Attach&lt;T&gt;()</c> - enabling <c>Update</c>/<c>Delete</c> to be called directly on a model
    /// received from Realtime.
    ///
    /// Left null, Realtime remains usable standalone: models still deserialize normally, just without client context.
    /// The Supabase umbrella client wires this automatically to its own Postgrest client.
    /// </summary>
    public IPostgrestClient? PostgrestClient { get; set; }

    /// <summary>
    /// Builds the WebSocket transport <see cref="RealtimeSocket"/> connects through. Left null, the default
    /// <see cref="System.Net.WebSockets.ClientWebSocket"/>-backed transport is used — the same behavior as
    /// before this option existed. Set this to swap in a platform-specific transport (e.g. a browser-native
    /// adapter for Unity IL2CPP/WebGL builds).
    /// </summary>
    public IWebSocketFactory? WebSocketFactory { get; set; }
}
