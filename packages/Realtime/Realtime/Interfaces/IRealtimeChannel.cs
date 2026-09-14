using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace Supabase.Realtime.Interfaces;

/// <summary>
/// A contract representing a realtime channel
/// </summary>
public interface IRealtimeChannel
{
    /// <summary>
    /// Delegate for messages received on this channel
    /// </summary>
    delegate void MessageReceivedHandler(IRealtimeChannel sender, SocketResponse message);

    /// <summary>
    /// Delegate for channel state changes
    /// </summary>
    delegate void StateChangedHandler(IRealtimeChannel sender, ChannelState state);

    /// <summary>
    /// Delegate for postgres changes
    /// </summary>
    delegate void PostgresChangesHandler(IRealtimeChannel sender, PostgresChangesResponse change);

    /// <summary>
    /// Delegate for errors on this channel
    /// </summary>
    delegate void ErrorEventHandler(IRealtimeChannel sender, RealtimeException exception);

    /// <summary>
    /// If this channel has been successfully joined (and thus, should be rejoined on a failure)
    /// </summary>
    bool HasJoinedOnce { get; }

    /// <summary>
    /// Is channel closed?
    /// </summary>
    bool IsClosed { get; }

    /// <summary>
    /// Is channel erroring?
    /// </summary>
    bool IsErrored { get; }

    /// <summary>
    /// Is channel joined?
    /// </summary>
    bool IsJoined { get; }

    /// <summary>
    /// Is channel being joined?
    /// </summary>
    bool IsJoining { get; }

    /// <summary>
    /// Is channel leaving?
    /// </summary>
    bool IsLeaving { get; }

    /// <summary>
    /// The Channel's initialization options
    /// </summary>
    ChannelOptions Options { get; }

    /// <summary>
    /// The Channel's broadcast options (used prior to <see cref="Subscribe"/>)
    /// </summary>
    BroadcastOptions? BroadcastOptions { get; }

    /// <summary>
    /// The Channel's presence options (used prior to <see cref="Subscribe"/>)
    /// </summary>
    PresenceOptions? PresenceOptions { get; }

    /// <summary>
    /// The Channel's postgres_changes options (used prior to <see cref="Subscribe"/>)
    /// </summary>
    List<PostgresChangesOptions> PostgresChangesOptions { get; }

    /// <summary>
    /// The Channel's current state
    /// </summary>
    ChannelState State { get; }

    /// <summary>
    /// A string representing this channel's topic, used for identifying/repeat access to this channel.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Add a state changed listener
    /// </summary>
    /// <param name="stateChangedHandler"></param>
    void AddStateChangedHandler(StateChangedHandler stateChangedHandler);

    /// <summary>
    /// Remove a state changed handler
    /// </summary>
    /// <param name="stateChangedHandler"></param>
    void RemoveStateChangedHandler(StateChangedHandler stateChangedHandler);

    /// <summary>
    /// Clear state changed handlers
    /// </summary>
    void ClearStateChangedHandlers();

    /// <summary>
    /// Add a message received handler
    /// </summary>
    /// <param name="messageReceivedHandler"></param>
    void AddMessageReceivedHandler(MessageReceivedHandler messageReceivedHandler);

    /// <summary>
    /// Remove a message received handler.
    /// </summary>
    /// <param name="messageReceivedHandler"></param>
    void RemoveMessageReceivedHandler(MessageReceivedHandler messageReceivedHandler);

    /// <summary>
    /// Clear message received handlers.
    /// </summary>
    void ClearMessageReceivedHandlers();

    /// <summary>
    /// Registers a postgres_changes listener and its handler in a single call, then returns the channel
    /// so registrations can be chained before <see cref="Subscribe"/>. This is the idiomatic replacement
    /// for pairing <see cref="Register(PostgresChangesOptions)"/> with
    /// <see cref="AddPostgresChangeHandler"/>.
    /// </summary>
    /// <param name="postgresChangeHandler">The handler invoked when a matching change is received.</param>
    /// <param name="listenType">The change type to listen for.</param>
    /// <param name="filter">
    /// Optional targeting — schema, table, and row filter. When omitted, listens to every table in the
    /// <c>public</c> schema.
    /// </param>
    /// <returns>The same channel, to allow chaining further registrations.</returns>
    /// <example>
    /// <code>
    /// await client.Channel("public:todos")
    ///     .OnPostgresChange(
    ///         (_, change) => Console.WriteLine(change.Model&lt;Todo&gt;()),
    ///         ListenType.Inserts,
    ///         new PostgresChangesFilter { Table = "todos" })
    ///     .Subscribe();
    /// </code>
    /// </example>
    IRealtimeChannel OnPostgresChange(PostgresChangesHandler postgresChangeHandler, ListenType listenType,
        PostgresChangesFilter? filter = null);

    /// <summary>
    /// Add a postgres_changes handler
    /// </summary>
    /// <param name="listenType"></param>
    /// <param name="postgresChangeHandler"></param>
    [Obsolete("Favor OnPostgresChange instead.")]
    void AddPostgresChangeHandler(ListenType listenType, PostgresChangesHandler postgresChangeHandler);

    /// <summary>
    /// Remove a postgres_changes handler
    /// </summary>
    /// <param name="listenType"></param>
    /// <param name="postgresChangeHandler"></param>
    void RemovePostgresChangeHandler(ListenType listenType, PostgresChangesHandler postgresChangeHandler);

    /// <summary>
    /// Clear postgres_changes handlers
    /// </summary>
    void ClearPostgresChangeHandlers();

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
    /// Get the <see cref="IRealtimeBroadcast"/> helper
    /// </summary>
    /// <returns></returns>
    IRealtimeBroadcast? Broadcast();

    /// <summary>
    /// Get the <see cref="IRealtimePresence"/> helper.
    /// </summary>
    /// <returns></returns>
    IRealtimePresence? Presence();

    /// <summary>
    /// Push an arbitrary event to a subscribed channel.
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="type"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    /// <returns></returns>
    Push Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DefaultTimeout);

    /// <summary>
    /// Rejoin a channel.
    /// </summary>
    /// <param name="timeoutMs"></param>
    void Rejoin(int timeoutMs = DefaultTimeout);

    /// <summary>
    /// Send an arbitrary event with an awaitable task.
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="type"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    /// <returns></returns>
    Task<bool> Send(ChannelEventName eventType, string? type, object payload, int timeoutMs = DefaultTimeout);

    /// <summary>
    /// Register broadcast options, must be called to use <see cref="IRealtimeBroadcast"/>, and prior to <see cref="Subscribe"/>
    /// </summary>
    /// <param name="broadcastSelf"></param>
    /// <param name="broadcastAck"></param>
    /// <typeparam name="TBroadcastResponse"></typeparam>
    /// <returns></returns>
    RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(bool broadcastSelf = false,
        bool broadcastAck = false) where TBroadcastResponse : BaseBroadcast;


    /// <summary>
    /// Registers the channel with the specified configuration options.
    /// </summary>
    /// <param name="options">The configuration options for the broadcast registration.</param>
    /// <typeparam name="TBroadcastResponse">The type of the broadcast response, which must inherit from <see cref="BaseBroadcast"/>.</typeparam>
    /// <returns>A <see cref="RealtimeBroadcast{TBroadcastResponse}"/> instance for managing the broadcast.</returns>
    RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(BroadcastOptions options)
        where TBroadcastResponse : BaseBroadcast;

    /// <summary>
    /// Register presence options, must be called to use <see cref="IRealtimePresence"/>, and prior to <see cref="Subscribe"/>
    /// </summary>
    /// <param name="presenceKey"></param>
    /// <typeparam name="TPresenceResponse"></typeparam>
    /// <returns></returns>
    RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(string presenceKey)
        where TPresenceResponse : BasePresence;

    /// <summary>
    /// Registers a <see cref="RealtimePresence{TPresenceResponse}"/> instance with the specified options.
    /// </summary>
    /// <typeparam name="TPresenceResponse">The model representing a presence payload.</typeparam>
    /// <param name="options">The presence options to configure the channel's behavior.</param>
    /// <returns>A <see cref="RealtimePresence{TPresenceResponse}"/> instance for managing presence state.</returns>
    RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(PresenceOptions options)
        where TPresenceResponse : BasePresence;

    /// <summary>
    /// Register postgres_changes options, must be called to use <see cref="PostgresChangesHandler"/>, and
    /// prior to <see cref="Subscribe"/>
    /// </summary>
    /// <param name="postgresChangesOptions"></param>
    /// <returns></returns>
    [Obsolete("Favor OnPostgresChange instead.")]
    IRealtimeChannel Register(PostgresChangesOptions postgresChangesOptions);

    /// <summary>
    /// Subscribes to a channel.
    /// </summary>
    /// <param name="timeoutMs"></param>
    /// <returns></returns>
    Task<IRealtimeChannel> Subscribe(int timeoutMs = DefaultTimeout);

    /// <summary>
    /// Unsubscribes from a channel.
    /// </summary>
    /// <returns></returns>
    IRealtimeChannel Unsubscribe();
}
