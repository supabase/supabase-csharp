using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase.Core.Interfaces;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces;

/// <summary>
/// Contract representing a Realtime Client
/// </summary>
/// <typeparam name="TSocket"></typeparam>
/// <typeparam name="TChannel"></typeparam>
public interface IRealtimeClient<TSocket, TChannel> : IGettableHeaders
    where TSocket : IRealtimeSocket
    where TChannel : IRealtimeChannel
{
    /// <summary>
    /// The options initializing this client.
    /// </summary>
    ClientOptions Options { get; }

    /// <summary>
    /// Json serializer settings
    /// </summary>
    JsonSerializerOptions SerializerSettings { get; }

    /// <summary>
    /// The connected realtime socket
    /// </summary>
    IRealtimeSocket? Socket { get; }

    /// <summary>
    /// The current state of the connection
    /// </summary>
    ConnectionState ConnectionState { get; }

    /// <summary>
    /// A collection of channels ordered by topic name
    /// </summary>
    ReadOnlyDictionary<string, TChannel> Subscriptions { get; }

    /// <summary>
    /// Delegate for handling a socket state event, this can be seen as synonymous with the Client's state.
    /// </summary>
    delegate void SocketStateEventHandler(IRealtimeClient<TSocket, TChannel> sender, SocketState state);

    /// <summary>
    /// Add a Socket State listener
    /// </summary>
    /// <param name="handler"></param>
    void AddStateChangedHandler(SocketStateEventHandler handler);

    /// <summary>
    /// Remove a Socket State listener
    /// </summary>
    /// <param name="handler"></param>
    void RemoveStateChangedHandler(SocketStateEventHandler handler);

    /// <summary>
    /// Clear socket state listeners
    /// </summary>
    void ClearStateChangedHandlers();

    /// <summary>
    /// Delegate for handling a heartbeat event, latency is only set on Ok and Error.
    /// </summary>
    delegate void HeartbeatEventHandler(IRealtimeClient<TSocket, TChannel> sender, HeartbeatStatus status, TimeSpan? latency);

    /// <summary>
    /// Add a heartbeat listener
    /// </summary>
    /// <param name="handler"></param>
    void AddHeartbeatHandler(HeartbeatEventHandler handler);

    /// <summary>
    /// Remove a heartbeat listener
    /// </summary>
    /// <param name="handler"></param>
    void RemoveHeartbeatHandler(HeartbeatEventHandler handler);

    /// <summary>
    /// Clear heartbeat listeners
    /// </summary>
    void ClearHeartbeatHandlers();

    /// <summary>
    /// Adds a debug handler, likely used within a logging solution of some kind.
    /// </summary>
    /// <param name="handler"></param>
    void AddDebugHandler(IRealtimeDebugger.DebugEventHandler handler);

    /// <summary>
    /// Removes a debug handler
    /// </summary>
    /// <param name="handler"></param>
    void RemoveDebugHandler(IRealtimeDebugger.DebugEventHandler handler);

    /// <summary>
    /// Clears debug handlers;
    /// </summary>
    void ClearDebugHandlers();

    /// <summary>
    /// Initialize a new channel with an arbitrary channel name.
    /// </summary>
    /// <param name="channelName"></param>
    /// <returns></returns>
    TChannel Channel(string channelName);

    /// <summary>
    /// Adds a RealtimeChannel subscription with custom options - if a subscription exists with the same signature, the existing subscription will be returned.
    /// </summary>
    /// <param name="channelName">The name of the Channel to join</param>
    /// <param name="options">Custom channel options for configuring the subscription</param>
    /// <returns>A RealtimeChannel instance representing the subscription</returns>
    /// <exception cref="Exception">Thrown when Socket is null, indicating Connect() was not called</exception>
    TChannel Channel(string channelName, ChannelOptions options);

    /// <summary>
    /// Shorthand initialization of a channel with postgres_changes options already set. 
    /// </summary>
    /// <param name="database"></param>
    /// <param name="schema"></param>
    /// <param name="table"></param>
    /// <param name="column"></param>
    /// <param name="value"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    TChannel Channel(string database = "realtime", string schema = "public", string table = "*",
        string? column = null, string? value = null, Dictionary<string, string>? parameters = null);

    /// <summary>
    /// Connect to the <see cref="Socket"/>
    /// </summary>
    /// <param name="callback"></param>
    /// <returns></returns>
    IRealtimeClient<TSocket, TChannel> Connect(
        Action<IRealtimeClient<TSocket, TChannel>, RealtimeException?>? callback = null);

    /// <summary>
    /// Connect to the <see cref="Socket"/>
    /// </summary>
    /// <returns></returns>
    Task<IRealtimeClient<TSocket, TChannel>> ConnectAsync();

    /// <summary>
    /// Disconnect from the <see cref="Socket"/>
    /// </summary>
    /// <param name="code"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    IRealtimeClient<TSocket, TChannel> Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure,
        string reason = "Programmatic Disconnect");

    /// <summary>
    /// Remove an initialized <see cref="IRealtimeChannel"/>
    /// </summary>
    /// <param name="channel"></param>
    void Remove(TChannel channel);

    /// <summary>
    /// Unsubscribes from every channel and disconnects from the <see cref="Socket"/>.
    /// </summary>
    void RemoveAll();

    /// <summary>
    /// Sets the authentication JWT to be passed into all realtime channels. Used for WALRUS permissions.
    /// </summary>
    /// <param name="jwt"></param>
    void SetAuth(string jwt);
}
