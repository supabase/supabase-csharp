using Websocket.Client;

namespace Supabase.Realtime.Exceptions;

/// <summary>
/// A failure hint
/// </summary>
public static class FailureHint
{
    /// <summary>
    /// Reasons for a failure
    /// </summary>
    public enum Reason
    {
        /// <summary>
        /// Catchall for any kind of failure that is presently untyped.
        /// </summary>
        Unknown,

        /// <summary>
        /// A push timeout
        /// </summary>
        PushTimeout,

        /// <summary>
        /// Channel is not open
        /// </summary>
        ChannelNotOpen,

        /// <summary>
        /// Channel cannot be joined
        /// </summary>
        ChannelJoinFailure,

        /// <summary>
        /// Socket has errored, either in connection or reconnection.
        /// </summary>
        SocketError,

        /// <summary>
        /// Connection has been lost
        /// </summary>
        ConnectionLost,

        /// <summary>
        /// No message has been received, usually given by server.
        /// If seen, please open an issue.
        /// </summary>
        ConnectionStale,

        /// <summary>
        /// Cannot make changes after subscribe
        /// </summary>
        StateInvalid,
    }

    /// <summary>
    /// Parses a Failure reason from a <see cref="DisconnectionInfo"/>
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public static Reason Parse(DisconnectionInfo info)
    {
        return info.Type switch
        {
            DisconnectionType.Error => Reason.SocketError,
            DisconnectionType.NoMessageReceived => Reason.ConnectionStale,
            DisconnectionType.Lost => Reason.ConnectionLost,
            DisconnectionType.ByServer => Reason.Unknown,
            _ => Reason.Unknown,
        };
    }
}
