using System;
using System.Threading.Tasks;
using Supabase.Realtime.Models;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces;

/// <summary>
/// A contract representing a realtime broadcast
/// </summary>
public interface IRealtimeBroadcast
{
    /// <summary>
    /// A delegate for broadcast events  
    /// </summary>
    delegate void BroadcastEventHandler(IRealtimeBroadcast sender, BaseBroadcast? broadcast);

    /// <summary>
    /// Adds a broadcast event handler
    /// </summary>
    /// <param name="broadcastEventHandler"></param>
    void AddBroadcastEventHandler(BroadcastEventHandler broadcastEventHandler);

    /// <summary>
    /// Removes a broadcast event handler
    /// </summary>
    /// <param name="broadcastEventHandler"></param>
    void RemoveBroadcastEventHandler(BroadcastEventHandler broadcastEventHandler);

    /// <summary>
    /// Clears all broadcast event handlers
    /// </summary>
    void ClearBroadcastEventHandlers();

    /// <summary>
    /// Sends a broadcast to a given event name with an arbitrary, serializable payload.
    /// </summary>
    /// <param name="broadcastEventName"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    /// <returns></returns>
    Task<bool> Send(string? broadcastEventName, object payload, int timeoutMs = DefaultTimeout);

    /// <summary>
    /// Sends a broadcast whose payload is bytes, in a binary frame. Needs a client that speaks
    /// <see cref="ProtocolVersion.V2"/>, and only subscribers that speak it too receive the broadcast; they read
    /// the bytes from <see cref="BaseBroadcast.BinaryPayload"/>.
    /// </summary>
    /// <param name="broadcastEventName">The event name, at most 255 bytes.</param>
    /// <param name="payload">The bytes to send.</param>
    /// <param name="timeoutMs">How long to wait for the acknowledgment, when the channel asked for one.</param>
    /// <exception cref="Supabase.Realtime.Exceptions.RealtimeException">The client speaks protocol 1.0.0, the channel topic is longer than 255 bytes, or the server rejected the broadcast or did not acknowledge it in time.</exception>
    /// <exception cref="ArgumentException">The event name is longer than 255 bytes.</exception>
    /// <example>
    /// <code>
    /// var broadcast = channel.Register&lt;BaseBroadcast&gt;();
    /// await channel.Subscribe();
    /// await broadcast.SendBinary("frame", bytes);
    /// </code>
    /// </example>
    Task SendBinary(string broadcastEventName, byte[] payload, int timeoutMs = DefaultTimeout);

    /// <summary>
    /// An internal trigger used for notifying event delegates.
    /// </summary>
    /// <param name="response"></param>
    internal void TriggerReceived(SocketResponse response);
}