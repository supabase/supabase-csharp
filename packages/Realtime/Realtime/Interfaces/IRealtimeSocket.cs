using Supabase.Realtime.Socket;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Interfaces;
using Supabase.Realtime.Exceptions;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces;

/// <summary>
/// Contract for a realtime socket.
/// </summary>
public interface IRealtimeSocket: IGettableHeaders
{
    /// <summary>
    /// Is this socket connected?
    /// </summary>
    bool IsConnected { get; }
        
    /// <summary>
    /// Delegate for errors on this socket
    /// </summary>
    delegate void ErrorEventHandler(IRealtimeSocket sender, RealtimeException exception);
        
    /// <summary>
    /// Delegate for handling socket state changes.
    /// </summary>
    delegate void StateEventHandler(IRealtimeSocket sender, SocketState state);

    /// <summary>
    /// Delegate for handling message received events.
    /// </summary>
    delegate void MessageEventHandler(IRealtimeSocket sender, SocketResponse message);

    /// <summary>
    /// Delegate for handling a heartbeat event.
    /// </summary>
    delegate void HeartbeatEventHandler(IRealtimeSocket sender, SocketResponse heartbeat);

    /// <summary>
    /// Add a state changed handler.
    /// </summary>
    /// <param name="handler"></param>
    void AddStateChangedHandler(StateEventHandler handler);

    /// <summary>
    /// Remove a state changed handler.
    /// </summary>
    /// <param name="handler"></param>
    void RemoveStateChangedHandler(StateEventHandler handler);

    /// <summary>
    /// Clear state changed handlers.
    /// </summary>
    void ClearStateChangedHandlers();

    /// <summary>
    /// Add a message received handler.
    /// </summary>
    /// <param name="handler"></param>
    void AddMessageReceivedHandler(MessageEventHandler handler);

    /// <summary>
    /// Remove a message received handler.
    /// </summary>
    /// <param name="handler"></param>
    void RemoveMessageReceivedHandler(MessageEventHandler handler);

    /// <summary>
    /// Clear message received handlers.
    /// </summary>
    void ClearMessageReceivedHandlers();

    /// <summary>
    /// Add a heartbeat handler.
    /// </summary>
    /// <param name="handler"></param>
    void AddHeartbeatHandler(HeartbeatEventHandler handler);

    /// <summary>
    /// Remove heartbeat handler.
    /// </summary>
    /// <param name="handler"></param>
    void RemoveHeartbeatHandler(HeartbeatEventHandler handler);

    /// <summary>
    /// Clear heartbeat handlers.
    /// </summary>
    void ClearHeartbeatHandlers();
        
    /// <summary>
    /// Add an error handler
    /// </summary>
    /// <param name="handler"></param>
    void AddErrorHandler(ErrorEventHandler handler);

    /// <summary>
    /// Remove an error handler
    /// </summary>
    /// <param name="handler"></param>
    void RemoveErrorHandler(ErrorEventHandler handler);

    /// <summary>
    /// Clear error handlers.
    /// </summary>
    void ClearErrorHandlers();

    /// <summary>
    /// Gets the roundtrip time of a single message between client and server.
    /// </summary>
    /// <returns></returns>
    Task<double> GetLatency();

    /// <summary>
    /// Connects to a socket
    /// </summary>
    /// <returns></returns>
    Task Connect();

    /// <summary>
    /// Disconnects from a socket
    /// </summary>
    /// <param name="code"></param>
    /// <param name="reason"></param>
    void Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "");

    /// <summary>
    /// Generates a Message ref, used in <see cref="Push"/>
    /// </summary>
    /// <returns></returns>
    string MakeMsgRef();
        
    /// <summary>
    /// Push a <see cref="SocketRequest"/> to the <see cref="Socket"/>
    /// </summary>
    /// <param name="data"></param>
    void Push(SocketRequest data);
        
    /// <summary>
    /// The phoenix specific reply event name for a message.
    /// </summary>
    /// <param name="msgRef"></param>
    /// <returns></returns>
    internal string ReplyEventName(string msgRef);

    /// <summary>
    /// Broadcasts through the Realtime REST API.
    /// </summary>
    internal Task HttpSendAsync(RealtimeChannel channel, string eventName, object payload, CancellationToken cancellationToken);
}