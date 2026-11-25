using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime.Socket;
using Supabase.Realtime.Sockets;

namespace Supabase.Realtime;

/// <summary>
/// Options used when initializing a <see cref="Client"/>
/// </summary>
public class ClientOptions
{
    private static readonly ThreadLocal<Random> RandomProvider = new(() => new Random(Guid.NewGuid().GetHashCode()));

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
    [ImplementedCapability("realtime.configuration.heartbeat_interval")]
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The version of the Realtime protocol the client speaks. <see cref="Supabase.Realtime.ProtocolVersion.V2"/>
    /// carries a broadcast whose payload is bytes, sent with <see cref="Interfaces.IRealtimeBroadcast.SendBinary"/>
    /// and read from <see cref="Models.BaseBroadcast.BinaryPayload"/>. Defaults to
    /// <see cref="Supabase.Realtime.ProtocolVersion.V1"/>.
    /// </summary>
    [ImplementedCapability("realtime.configuration.binary_protocol")]
    public ProtocolVersion ProtocolVersion { get; set; } = ProtocolVersion.V1;

    /// <summary>
    /// How long <c>ProbeConnection</c> waits for the heartbeat reply before the socket reconnects. Defaults to 5 seconds.
    /// </summary>
    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// When set, the socket disconnects once the last channel has been closed for this long, and the next
    /// <c>Subscribe</c> connects again. Left null, the socket stays open.
    /// </summary>
    [PartialCapability("realtime.configuration.deferred_disconnect", "Opt-in: null by default, so the socket stays open. Closing an idle socket by default is a breaking change planned for 9.0.0.")]
    public TimeSpan? DisconnectOnEmptyChannelsAfter { get; set; }

    /// <summary>
    /// The delay before the next reconnect attempt after one failed, given the attempt number starting at 1. Defaults
    /// to a random delay of up to 1, 2, 4, 8 and 16 seconds for the first attempts, then up to 30 seconds.
    /// </summary>
    [ImplementedCapability("realtime.configuration.reconnect_backoff")]
    public Func<int, TimeSpan> ReconnectAfterInterval { get; set; } = attempt =>
        TimeSpan.FromSeconds(RandomProvider.Value!.NextDouble() * Math.Min(30, Math.Pow(2, attempt - 1)));

    /// <summary>
    /// Whether the client watches the platform's network monitor and, when the network comes back, reconnects without
    /// waiting out the rest of the reconnect delay. Has no effect on a runtime without that monitor. Defaults to true.
    /// </summary>
    [ImplementedCapability("realtime.configuration.network_path_wake", "NetworkChange.NetworkAddressChanged is the monitor; the option has no effect on a runtime that does not support it.")]
    public bool MonitorsNetworkPath { get; set; } = true;

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
    [ImplementedCapability("realtime.configuration.custom_websocket_transport")]
    public IWebSocketFactory? WebSocketFactory { get; set; }

    /// <summary>
    /// Returns the current access token. Called on connect and on every heartbeat, so a refreshed token reaches
    /// the server without calling <c>SetAuth</c>. A null result keeps the current token. A failure on connect
    /// propagates out of <c>ConnectAsync</c>; a failure on a heartbeat is logged and keeps the current token.
    /// </summary>
    [ImplementedCapability("realtime.configuration.access_token_callback")]
    public Func<Task<string?>>? RetrieveAccessToken { get; set; }

    /// <summary>
    /// Receives the socket's lifecycle diagnostics (connected, disconnected, reconnecting, heartbeat timeout) and every
    /// channel's state changes at <c>Debug</c>. Left null, the client logs nothing. Never carries credentials, tokens,
    /// payloads or the endpoint's query string.
    /// </summary>
    [ImplementedCapability("realtime.configuration.custom_logger")]
    public Microsoft.Extensions.Logging.ILogger? Logger { get; set; }

    internal bool IsProtocolVersionV2() => this.ProtocolVersion == ProtocolVersion.V2;
}
