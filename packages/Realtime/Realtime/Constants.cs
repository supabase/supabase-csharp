using Supabase.Core.Attributes;
using Supabase.Realtime.Socket;

namespace Supabase.Realtime;

/// <summary>
/// Shared constants for Realtime
/// </summary>
public static class Constants
{
    /// <summary>
    /// The Current Socket state, used in <see cref="RealtimeSocket"/>
    /// </summary>
    public enum SocketState
    {
        /// <summary>
        /// Socket Open
        /// </summary>
        Open,

        /// <summary>
        /// Socket Closed
        /// </summary>
        Close,

        /// <summary>
        /// Socket is Reconnecting
        /// </summary>
        Reconnect,

        /// <summary>
        /// Socket has errored
        /// </summary>
        Error
    }

    /// <summary>
    /// The heartbeat status, used in <see cref="RealtimeSocket"/>
    /// </summary>
    public enum HeartbeatStatus
    {
        /// <summary>
        /// Heartbeat sent
        /// </summary>
        Sent,

        /// <summary>
        /// Heartbeat answered
        /// </summary>
        Ok,

        /// <summary>
        /// Heartbeat rejected
        /// </summary>
        Error,

        /// <summary>
        /// Heartbeat timed out, socket reconnects
        /// </summary>
        Timeout
    }

    /// <summary>
    /// Mapping of channel states, used with <see cref="RealtimeChannel"/>
    /// </summary>
    public enum ChannelState
    {
        /// <summary>
        /// Channel is closed
        /// </summary>
        [MapTo("closed")] Closed,

        /// <summary>
        /// Channel has errored
        /// </summary>
        [MapTo("errored")] Errored,

        /// <summary>
        /// Channel is joined
        /// </summary>
        [MapTo("joined")] Joined,

        /// <summary>
        /// Channel is joining
        /// </summary>
        [MapTo("joining")] Joining,

        /// <summary>
        /// Channel is leaving
        /// </summary>
        [MapTo("leaving")] Leaving
    }

    /// <summary>
    /// A channel event type used and parsed in a <see cref="SocketResponse"/>
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// postgres_changes `Insert` event
        /// </summary>
        Insert,

        /// <summary>
        /// postgres_changes `Update` event
        /// </summary>
        Update,

        /// <summary>
        /// postgres_changes `Delete` event
        /// </summary>
        Delete,

        /// <summary>
        /// A broadcast event
        /// </summary>
        Broadcast,

        /// <summary>
        /// A presence `state` or `sync` event
        /// </summary>
        PresenceState,

        /// <summary>
        /// A presence `leave` or `join` event
        /// </summary>
        PresenceDiff,

        /// <summary>
        /// The catchall event for `postgres_changes`, parsed into a more specific `Insert`, `Update` or `Delete`
        /// </summary>
        PostgresChanges,

        /// <summary>
        /// A system event (likely unused by the developer)
        /// </summary>
        System,

        /// <summary>
        /// An internal event (likely unused by the developer)
        /// </summary>
        Internal,

        /// <summary>
        /// A presently unknown event, if this is seen, please open an issue at https://github.com/supabase-community/realtime-csharp
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Map of Presence listener types, used with: <see cref="RealtimePresence{TPresenceModel}"/>
    /// </summary>
    public enum PresenceListenEventTypes
    {
        /// <summary>
        /// A sync event (either join or leave)
        /// </summary>
        [MapTo("sync")] Sync,

        /// <summary>
        /// A join event
        /// </summary>
        [MapTo("join")] Join,

        /// <summary>
        /// A leave event
        /// </summary>
        [MapTo("leave")] Leave
    }

    /// <summary>
    /// Mapping for event names that can be used with <see cref="RealtimeChannel.Push"/> to send arbitrary data.
    /// This is unlikely to be used by the developer.
    /// </summary>
    public enum ChannelEventName
    {
        /// <summary>
        /// The broadcast event
        /// </summary>
        [MapTo("broadcast")] Broadcast,

        /// <summary>
        /// The Presence event
        /// </summary>
        [MapTo("presence")] Presence,

        /// <summary>
        /// A postgres_changes event
        /// </summary>
        [MapTo("postgres_changes")] PostgresChanges
    }

    /// <summary>
    /// Timeout interval for requests (used in Socket and Push)
    /// </summary>
    public const int DefaultTimeout = 10000;

    /// <summary>
    /// Phoenix Socket Server Event: CLOSE
    /// </summary>
    public static string ChannelEventClose = "phx_close";

    /// <summary>
    /// Phoenix Socket Server Event: ERROR
    /// </summary>
    public static string ChannelEventError = "phx_error";

    /// <summary>
    /// Phoenix Socket Server Event: JOIN
    /// </summary>
    public const string ChannelEventJoin = "phx_join";

    /// <summary>
    /// Phoenix Socket Server Event: REPLY
    /// </summary>
    public const string ChannelEventReply = "phx_reply";

    /// <summary>
    /// Phoenix Socket Server Event: SYSTEM
    /// </summary>
    public const string ChannelEventSystem = "system";

    /// <summary>
    /// Phoenix Socket Server Event: LEAVE
    /// </summary>
    public const string ChannelEventLeave = "phx_leave";

    /// <summary>
    /// Phoenix Server Event: OK
    /// </summary>
    public const string PhoenixStatusOk = "ok";

    /// <summary>
    /// Phoenix Server Event: POSTGRES_CHANGES
    /// </summary>
    public const string ChannelEventPostgresChanges = "postgres_changes";

    /// <summary>
    /// Phoenix Server Event: BROADCAST
    /// </summary>
    public const string ChannelEventBroadcast = "broadcast";

    /// <summary>
    /// Phoenix Server Event: PRESENCE_STATE
    /// </summary>
    public const string ChannelEventPresenceState = "presence_state";

    /// <summary>
    /// Phoenix Server Event: PRESENCE_DIFF
    /// </summary>
    public const string ChannelEventPresenceDiff = "presence_diff";

    /// <summary>
    /// Phoenix Server Event: ERROR
    /// </summary>
    public const string PhoenixStatusError = "error";

    /// <summary>
    /// The transport type, used with Phoenix server implementations and appended on the <see cref="RealtimeSocket.EndpointUrl"/>
    /// </summary>
    public const string TransportWebsocket = "websocket";

    /// <summary>
    /// The event name used to send an access_token to the Phoenix server
    /// </summary>
    public const string ChannelAccessToken = "access_token";
}