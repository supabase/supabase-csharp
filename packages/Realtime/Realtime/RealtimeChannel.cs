using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using Supabase.Realtime.Socket.Responses;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.Interfaces.IRealtimeChannel;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using Timer = System.Timers.Timer;

// ReSharper disable InvalidXmlDocComment

[assembly: InternalsVisibleTo("Realtime.Tests")]

namespace Supabase.Realtime;

/// <summary>
/// Class representation of a channel subscription
/// </summary>
public class RealtimeChannel : IRealtimeChannel
{
    /// <summary>
    /// As to whether this Channel is Closed
    /// </summary>
    public bool IsClosed => this.State == ChannelState.Closed;

    /// <summary>
    /// As to if this Channel has Errored
    /// </summary>
    public bool IsErrored => this.State == ChannelState.Errored;

    /// <summary>
    /// As to if this Channel is currently Joined
    /// </summary>
    public bool IsJoined => this.State == ChannelState.Joined;

    /// <summary>
    /// As to if this Channel is currently Joining
    /// </summary>
    public bool IsJoining => this.State == ChannelState.Joining;

    /// <summary>
    /// As to if this channel is currently leaving
    /// </summary>
    public bool IsLeaving => this.State == ChannelState.Leaving;

    /// <summary>
    /// The channel's topic (identifier)
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// The Channel's current state.
    /// </summary>
    public ChannelState State { get; private set; } = ChannelState.Closed;

    /// <summary>
    /// Options passed to this channel instance.
    /// </summary>
    public ChannelOptions Options { get; }

    /// <summary>
    /// The saved Broadcast Options, set in <see cref="Register{TBroadcastResponse}(bool, bool)"/>
    /// </summary>
    public BroadcastOptions? BroadcastOptions { get; private set; } = new();

    /// <summary>
    /// The saved Presence Options, set in <see cref="Register{TPresenceResponse}(string)"/> or <see cref="Register{TPresenceResponse}(PresenceOptions)"/>
    /// </summary>
    public PresenceOptions? PresenceOptions { get; private set; }

    /// <summary>
    /// The saved Postgres Changes Options, set in <see cref="Register(PostgresChanges.PostgresChangesOptions)"/>
    /// </summary>
    public List<PostgresChangesOptions> PostgresChangesOptions { get; } = new();

    /// <summary>
    /// Flag stating whether a channel has been joined once or not.
    /// </summary>
    public bool HasJoinedOnce { get; private set; }

    /// <summary>
    /// Flag stating if a channel is currently subscribed.
    /// </summary>
    public bool IsSubscribed;

    /// <summary>
    /// Returns the <see cref="IRealtimeBroadcast"/> instance.
    /// </summary>
    /// <returns></returns>
    public IRealtimeBroadcast? Broadcast() => this.broadcast;

    /// <summary>
    /// Returns a typed <see cref="RealtimeBroadcast{TBroadcastModel}" /> instance.
    /// </summary>
    /// <typeparam name="TBroadcastModel"></typeparam>
    /// <returns></returns>
    public RealtimeBroadcast<TBroadcastModel>? Broadcast<TBroadcastModel>() where TBroadcastModel : BaseBroadcast =>
        this.broadcast != null ? (RealtimeBroadcast<TBroadcastModel>) this.broadcast : default;

    /// <summary>
    /// Returns the <see cref="IRealtimePresence"/> instance.
    /// </summary>
    /// <returns></returns>
    public IRealtimePresence? Presence() => this.presence;

    /// <summary>
    /// Returns a typed <see cref="RealtimePresence{T}"/> instance.
    /// </summary>
    /// <typeparam name="TPresenceModel">Model representing a Presence payload</typeparam>
    /// <returns></returns>
    public RealtimePresence<TPresenceModel>? Presence<TPresenceModel>() where TPresenceModel : BasePresence =>
        this.presence != null ? (RealtimePresence<TPresenceModel>) this.presence : default;

    /// <summary>
    /// The initial request to join a channel (repeated on channel disconnect)
    /// </summary>
    internal Push? JoinPush;

    internal Push? LastPush;

    // Event handlers that pass events to typed instances for broadcast and presence.
    internal delegate void BroadcastEventHandler(IRealtimeChannel sender, SocketResponse response);

    internal delegate void PresenceDiffHandler(IRealtimeChannel sender, SocketResponse response);

    internal delegate void PresenceSyncHandler(IRealtimeChannel sender, SocketResponse response);

    internal BroadcastEventHandler? BroadcastHandler;
    internal PresenceDiffHandler? PresenceDiff;
    internal PresenceSyncHandler? PresenceSync;

    /// <summary>
    /// Buffer of Pushes held because of Socket availability
    /// </summary>
    private readonly List<Push> buffer = new();

    internal readonly IRealtimeSocket Socket;
    private IRealtimePresence? presence;
    private IRealtimeBroadcast? broadcast;
    private RealtimeException? exception;

    private readonly List<StateChangedHandler> stateChangedHandlers = new();
    private readonly List<MessageReceivedHandler> messageReceivedHandlers = new();
    private readonly List<ErrorEventHandler> errorEventHandlers = new();

    private bool CanPush => this.IsJoined && this.Socket.IsConnected;
    private bool hasJoinedOnce;
    private readonly Timer rejoinTimer;
    private bool isRejoining;

    private List<Binding> bindings = [];

    /// <summary>
    /// Initializes a Channel - must call `Subscribe()` to receive events.
    /// </summary>
    public RealtimeChannel(IRealtimeSocket socket, string channelName, ChannelOptions options)
    {
        this.Topic = channelName;
        this.Options = options;
        this.Options.Parameters ??= new Dictionary<string, string>();

        this.Socket = socket;
        this.Socket.AddStateChangedHandler(this.HandleSocketStateChanged);

        this.rejoinTimer = new Timer(options.ClientOptions.Timeout.TotalMilliseconds);
        this.rejoinTimer.Elapsed += this.HandleRejoinTimerElapsed;
        this.rejoinTimer.AutoReset = true;
    }

    /// <summary>
    /// Handles socket state changes, specifically when a socket reconnects this channel (if previously subscribed)
    /// should also rejoin.
    /// </summary>
    /// <param name="_"></param>
    /// <param name="state"></param>
    private void HandleSocketStateChanged(IRealtimeSocket _, SocketState state)
    {
        if (state != SocketState.Reconnect || !this.IsSubscribed) return;

        this.Rejoin();
    }

    /// <summary>
    /// Registers a <see cref="RealtimeBroadcast{TBroadcastModel}"/> instance - allowing broadcast responses to be parsed.
    /// </summary>
    /// <typeparam name="TBroadcastResponse"></typeparam>
    /// <param name="broadcastSelf">enables client to receive message it has broadcast</param>
    /// <param name="broadcastAck">instructs server to acknowledge that broadcast message was received</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(bool broadcastSelf = false,
        bool broadcastAck = false) where TBroadcastResponse : BaseBroadcast =>
        this.Register<TBroadcastResponse>(new BroadcastOptions(broadcastSelf, broadcastAck));

    /// <summary>
    /// Registers the channel for broadcast with the specified options.
    /// </summary>
    /// <typeparam name="TBroadcastResponse">The type of the broadcast response, which must inherit from <see cref="BaseBroadcast"/>.</typeparam>
    /// <param name="options">The broadcast options to configure the channel's broadcast behavior.</param>
    /// <returns>Returns an instance of <see cref="RealtimeBroadcast{TBroadcastResponse}"/> initialized with the specified broadcast options.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the method is called multiple times for the same channel.</exception>
    public RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(BroadcastOptions options) where TBroadcastResponse : BaseBroadcast
    {
        if (this.broadcast != null)
            throw new InvalidOperationException(
                "Register can only be called with broadcast options for a channel once.");

        if (!this.Options.IsPrivate && options.Replay != null)
            throw new InvalidOperationException(
                $"Broadcast replay requires a private channel, but '{this.Topic}' is public.");

        this.BroadcastOptions = options;

        var instance =
            new RealtimeBroadcast<TBroadcastResponse>(this, this.BroadcastOptions, this.Options.SerializerSettings);
        this.broadcast = instance;

        this.BroadcastHandler = (_, response) => this.broadcast.TriggerReceived(response);

        return instance;
    }

    /// <summary>
    /// Registers a <see cref="RealtimePresence{TPresenceResponse}"/> instance - allowing presence responses to be parsed and state to be tracked.
    /// </summary>
    /// <typeparam name="TPresenceResponse">The model representing a presence payload.</typeparam>
    /// <param name="presenceKey">used to track presence payload across clients</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if called multiple times.</exception>
    public RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(string presenceKey)
        where TPresenceResponse : BasePresence =>
        this.Register<TPresenceResponse>(PresenceOptions.WithPresence(presenceKey));

    /// <summary>
    /// Registers a <see cref="RealtimePresence{TPresenceResponse}"/> instance with the specified options.
    /// </summary>
    /// <typeparam name="TPresenceResponse">The model representing a presence payload.</typeparam>
    /// <param name="options">The presence options to configure the channel's presence behavior.</param>
    /// <returns>A <see cref="RealtimePresence{TPresenceResponse}"/> instance for managing presence state.</returns>
    /// <exception cref="InvalidOperationException">Thrown if called multiple times.</exception>
    public RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(PresenceOptions options)
        where TPresenceResponse : BasePresence
    {
        if (this.presence != null)
            throw new InvalidOperationException(
                "Register can only be called with presence options for a channel once.");

        this.PresenceOptions = options;
        var instance = new RealtimePresence<TPresenceResponse>(this, options, this.Options.SerializerSettings);
        this.presence = instance;

        this.PresenceSync = (_, response) => this.presence.TriggerSync(response);
        this.PresenceDiff = (_, response) => this.presence.TriggerDiff(response);

        return instance;
    }

    /// <summary>
    /// Registers a state changed listener relative to this channel. Called when channel state changes.
    /// </summary>
    /// <param name="stateChangedHandler"></param>
    public void AddStateChangedHandler(StateChangedHandler stateChangedHandler)
    {
        if (!this.stateChangedHandlers.Contains(stateChangedHandler))
            this.stateChangedHandlers.Add(stateChangedHandler);
    }

    /// <summary>
    /// Removes a channel state changed listener
    /// </summary>
    /// <param name="stateChangedHandler"></param>
    public void RemoveStateChangedHandler(StateChangedHandler stateChangedHandler)
    {
        if (this.stateChangedHandlers.Contains(stateChangedHandler))
            this.stateChangedHandlers.Remove(stateChangedHandler);
    }

    /// <summary>
    /// Clears all channel state changed listeners
    /// </summary>
    public void ClearStateChangedHandlers() =>
        this.stateChangedHandlers.Clear();

    /// <summary>
    /// Notifies registered listeners that a channel state has changed.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="shouldRejoin"></param>
    private void NotifyStateChanged(ChannelState state, bool shouldRejoin = true)
    {
        this.State = state;

        this.isRejoining = shouldRejoin;
        if (shouldRejoin)
            this.rejoinTimer.Start();
        else
            this.rejoinTimer.Stop();

        foreach (var handler in this.stateChangedHandlers.ToArray())
            handler.Invoke(this, state);
    }

    /// <summary>
    /// Registers a message received listener, called when a socket message is received for this channel.
    /// </summary>
    /// <param name="messageReceivedHandler"></param>
    public void AddMessageReceivedHandler(MessageReceivedHandler messageReceivedHandler)
    {
        if (!this.messageReceivedHandlers.Contains(messageReceivedHandler))
            this.messageReceivedHandlers.Add(messageReceivedHandler);
    }

    /// <summary>
    /// Removes a message received listener.
    /// </summary>
    /// <param name="messageReceivedHandler"></param>
    public void RemoveMessageReceivedHandler(MessageReceivedHandler messageReceivedHandler)
    {
        if (this.messageReceivedHandlers.Contains(messageReceivedHandler))
            this.messageReceivedHandlers.Remove(messageReceivedHandler);
    }

    /// <summary>
    /// Clears message received listeners.
    /// </summary>
    public void ClearMessageReceivedHandlers() =>
        this.messageReceivedHandlers.Clear();

    /// <summary>
    /// Notifies registered listeners that a channel message has been received.
    /// </summary>
    /// <param name="message"></param>
    private void NotifyMessageReceived(SocketResponse message)
    {
        foreach (var handler in this.messageReceivedHandlers.ToArray())
            handler.Invoke(this, message);
    }

    /// <inheritdoc />
    public IRealtimeChannel OnPostgresChange(PostgresChangesHandler postgresChangeHandler, ListenType listenType,
        PostgresChangesFilter? filter = null)
    {
        filter ??= new PostgresChangesFilter();
        this.RegisterPostgresChangesOptions(new PostgresChangesOptions(filter.Schema, filter.Table, listenType, filter.Filter));
        this.BindPostgresChangesHandler(listenType, postgresChangeHandler);
        return this;
    }

    /// <summary>
    /// Add a postgres changes listener. Should be paired with <see cref="Register"/>.
    /// </summary>
    /// <param name="listenType">The type of event this callback should process.</param>
    /// <param name="postgresChangeHandler"></param>
    [Obsolete("Favor OnPostgresChange instead.")]
    public void AddPostgresChangeHandler(ListenType listenType, PostgresChangesHandler postgresChangeHandler) => this.BindPostgresChangesHandler(listenType, postgresChangeHandler);

    /// <summary>
    /// Removes a postgres changes listener.
    /// </summary>
    /// <param name="listenType">The type of event this callback was registered to process.</param>
    /// <param name="postgresChangeHandler"></param>
    public void RemovePostgresChangeHandler(ListenType listenType, PostgresChangesHandler postgresChangeHandler) => this.RemovePostgresChangesFromBinding(listenType, postgresChangeHandler);

    /// <summary>
    /// Clears all postgres changes listeners.
    /// </summary>
    public void ClearPostgresChangeHandlers() => this.bindings.Clear();

    /// <summary>
    /// Adds an error event handler.
    /// </summary>
    /// <param name="handler"></param>
    public void AddErrorHandler(ErrorEventHandler handler)
    {
        if (!this.errorEventHandlers.Contains(handler))
            this.errorEventHandlers.Add(handler);
    }

    /// <summary>
    /// Removes an error event handler
    /// </summary>
    /// <param name="handler"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void RemoveErrorHandler(ErrorEventHandler handler)
    {
        if (this.errorEventHandlers.Contains(handler))
            this.errorEventHandlers.Remove(handler);
    }

    /// <summary>
    /// Clears Error Event Handlers
    /// </summary>
    public void ClearErrorHandlers() =>
        this.errorEventHandlers.Clear();

    private void NotifyErrorOccurred(RealtimeException exception)
    {
        this.exception = exception;

        this.NotifyStateChanged(ChannelState.Errored);

        foreach (var handler in this.errorEventHandlers)
            handler.Invoke(this, exception);
    }

    /// <summary>
    /// Notifies listeners of a postgres change message being received.
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="response"></param>
    private void NotifyPostgresChanges(EventType eventType, PostgresChangesResponse response)
    {
        var listenType = eventType switch
        {
            EventType.Insert => ListenType.Inserts,
            EventType.Delete => ListenType.Deletes,
            EventType.Update => ListenType.Updates,
            _ => ListenType.All
        };

        this.InvokeProperlyHandlerFromBind(listenType, response);
    }

    /// <summary>
    /// Registers postgres_changes options, can be called multiple times.
    ///
    /// Should be paired with <see cref="AddPostgresChangeHandler"/>
    /// </summary>
    /// <param name="postgresChangesOptions"></param>
    /// <returns></returns>
    [Obsolete("Favor OnPostgresChange instead.")]
    public IRealtimeChannel Register(PostgresChangesOptions postgresChangesOptions)
    {
        this.RegisterPostgresChangesOptions(postgresChangesOptions);
        return this;
    }

    /// <summary>
    /// Records postgres_changes options for this channel and binds them, so both <see cref="Register"/>
    /// and <see cref="OnPostgresChange"/> share a single registration path.
    /// </summary>
    /// <param name="postgresChangesOptions"></param>
    internal void RegisterPostgresChangesOptions(PostgresChangesOptions postgresChangesOptions)
    {
        if (this.IsJoined || this.IsJoining)
            throw new RealtimeException(
                $"Cannot add `postgres_changes` callbacks for {this.Topic} after `Subscribe()`.")
            {
                Reason = FailureHint.Reason.StateInvalid,
            };

        this.PostgresChangesOptions.Add(postgresChangesOptions);
        this.BindPostgresChangesOptions(postgresChangesOptions);
    }

    /// <summary>
    /// Subscribes to the channel given supplied Options/params.
    /// </summary>
    /// <param name="timeoutMs"></param>
    public Task<IRealtimeChannel> Subscribe(int timeoutMs = DefaultTimeout)
    {
        var tsc = new TaskCompletionSource<IRealtimeChannel>();

        if (this.IsSubscribed)
            return Task.FromResult(this as IRealtimeChannel);

        this.JoinPush = this.GenerateJoinPush();
        StateChangedHandler? channelCallback = null;
        EventHandler? joinPushTimeoutCallback = null;

        channelCallback = (sender, state) =>
        {
            switch (state)
            {
                // Success!
                case ChannelState.Joined:
                    this.HasJoinedOnce = true;
                    this.IsSubscribed = true;

                    sender.RemoveStateChangedHandler(channelCallback!);
                    this.JoinPush.OnTimeout -= joinPushTimeoutCallback;

                    // Clear buffer
                    foreach (var item in this.buffer)
                        item.Send();
                    this.buffer.Clear();

                    tsc.TrySetResult(this);
                    break;
                // Failure
                case ChannelState.Closed:
                case ChannelState.Errored:
                    sender.RemoveStateChangedHandler(channelCallback!);
                    this.JoinPush.OnTimeout -= joinPushTimeoutCallback;
                    tsc.TrySetException(this.exception);
                    break;
            }
        };

        // Throw an exception if there is a problem receiving a join response
        joinPushTimeoutCallback = (_, _) =>
        {
            this.RemoveStateChangedHandler(channelCallback);
            this.JoinPush.OnTimeout -= joinPushTimeoutCallback;

            this.NotifyErrorOccurred(new RealtimeException("Push Timeout")
            {
                Reason = FailureHint.Reason.PushTimeout
            });
        };

        this.AddStateChangedHandler(channelCallback);

        // Set a flag to prevent multiple join attempts.
        this.hasJoinedOnce = true;

        // Init and send join.
        this.Rejoin(timeoutMs);
        this.JoinPush.OnTimeout += joinPushTimeoutCallback;

        return tsc.Task;
    }

    /// <summary>
    /// Unsubscribes from the channel.
    /// </summary>
    public IRealtimeChannel Unsubscribe()
    {
        this.IsSubscribed = false;

        this.NotifyStateChanged(ChannelState.Leaving);

        var leavePush = new Push(this.Socket, this, ChannelEventLeave);
        leavePush.Send();

        this.NotifyStateChanged(ChannelState.Closed, false);

        return this;
    }

    /// <summary>
    /// Sends a `Push` request under this channel.
    ///
    /// Maintains a buffer in the event push is called prior to the channel being joined.
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="type"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    public Push Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DefaultTimeout)
    {
        if (!this.hasJoinedOnce)
        {
            throw new RealtimeException(
                $"Tried to push '{eventName}' to '{this.Topic}' before joining. Use `Channel.Subscribe()` before pushing events")
            {
                Reason = FailureHint.Reason.ChannelNotOpen
            };
        }

        var push = new Push(this.Socket, this, eventName, type, payload, timeoutMs);
        this.Enqueue(push);

        return push;
    }

    /// <summary>
    /// Sends an arbitrary payload with a given payload type (<see cref="ChannelEventName"/>)
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="type"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    public Task<bool> Send(ChannelEventName eventName, string? type, object payload, int timeoutMs = DefaultTimeout)
    {
        var push = this.Push(Core.Helpers.GetMappedToAttr(eventName).Mapping, type, payload, timeoutMs);
        // The server only sends a `phx_reply` for a `broadcast` push when the channel joined
        // with `config.broadcast.ack = true`. Without that, no reply will ever arrive, so
        // waiting for one would hang forever - resolve as soon as the push has been dispatched.
        return eventName == ChannelEventName.Broadcast && this.BroadcastOptions?.BroadcastAck != true
            ? Task.FromResult(true)
            : PushAwaiter.Await(push);
    }

    /// <summary>
    /// Awaits a reply for a given <see cref="Channel.Push"/>, resolving with whether a known reply was
    /// received, or faulting with a <see cref="RealtimeException"/> if the push times out first.
    ///
    /// Kept as instance methods (rather than local lambdas referencing each other) so there's no
    /// self/mutually-referencing closure over locals that get assigned after the closure is created.
    /// </summary>
    private sealed class PushAwaiter
    {
        private readonly Push push;
        private readonly TaskCompletionSource<bool> taskCompletion = new();

        private PushAwaiter(Push push)
        {
            this.push = push;
            this.push.AddMessageReceivedHandler(this.HandleMessageReceived);
            this.push.OnTimeout += this.HandleTimeout;
        }

        public static Task<bool> Await(Push push) => new PushAwaiter(push).taskCompletion.Task;

        private void HandleMessageReceived(IRealtimePush<RealtimeChannel, SocketResponse> sender, SocketResponse message)
        {
            this.Detach();
            this.taskCompletion.TrySetResult(message.Event != EventType.Unknown);
        }

        private void HandleTimeout(object sender, EventArgs e)
        {
            this.Detach();
            this.taskCompletion.TrySetException(new RealtimeException("Push Timeout") { Reason = FailureHint.Reason.PushTimeout });
        }

        private void Detach()
        {
            this.push.RemoveMessageReceivedHandler(this.HandleMessageReceived);
            this.push.OnTimeout -= this.HandleTimeout;
        }
    }

    /// <summary>
    /// Rejoins the channel.
    /// </summary>
    /// <param name="timeoutMs"></param>
    public void Rejoin(int timeoutMs = DefaultTimeout)
    {
        if (this.IsLeaving) return;
        this.SendJoin(timeoutMs);
    }

    /// <summary>
    /// Enqueues a message.
    /// </summary>
    /// <param name="push"></param>
    internal void Enqueue(Push push)
    {
        this.LastPush = push;

        if (this.CanPush)
        {
            this.LastPush.Send();
        }
        else
        {
            this.LastPush.StartTimeout();
            this.buffer.Add(this.LastPush);
        }
    }

    /// <summary>
    /// Generates the Join Push message by merging broadcast, presence, and postgres_changes options.
    /// </summary>
    /// <returns></returns>
    internal Push GenerateJoinPush() => new(this.Socket, this, ChannelEventJoin,
        payload: this.Options.IsPrivate
            ? Channel.JoinPush.ForPrivateChannel(this.BroadcastOptions, this.PresenceOptions, this.PostgresChangesOptions, this.Options.RetrieveAccessToken())
            : Channel.JoinPush.ForPublicChannel(this.BroadcastOptions, this.PresenceOptions, this.PostgresChangesOptions, this.Options.RetrieveAccessToken()));

    /// <summary>
    /// Generates an auth push.
    /// </summary>
    /// <returns></returns>
    private Push? GenerateAuthPush()
    {
        var accessToken = this.Options.RetrieveAccessToken();

        if (!string.IsNullOrEmpty(accessToken))
        {
            return new Push(this.Socket, this, ChannelAccessToken, payload: new Dictionary<string, string>
            {
                { "access_token", accessToken! }
            });
        }

        return null;
    }

    /// <summary>
    /// If the channel errors internally (phoenix error, not transport) attempt rejoining.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleRejoinTimerElapsed(object sender, ElapsedEventArgs e)
    {
        if (this.isRejoining) return;
        this.isRejoining = true;

        if (this.State != ChannelState.Closed && this.State != ChannelState.Errored)
            return;

        Debugger.Instance.Log(this, $"Rejoin Timer Elapsed: Attempting to rejoin [{this.Topic}]");

        // Reset join push instance
        this.JoinPush = this.GenerateJoinPush();

        this.Rejoin();
    }

    /// <summary>
    /// Sends the phoenix server a join message.
    /// </summary>
    /// <param name="timeoutMs"></param>
    private void SendJoin(int timeoutMs = DefaultTimeout)
    {
        this.NotifyStateChanged(ChannelState.Joining);

        // Remove handler if exists
        this.JoinPush?.RemoveMessageReceivedHandler(this.HandleJoinResponse);

        this.JoinPush = this.GenerateJoinPush();
        this.JoinPush.AddMessageReceivedHandler(this.HandleJoinResponse);
        this.JoinPush.Resend(timeoutMs);
    }

    /// <summary>
    /// Handles a received join response (received after sending on subscribe/reconnection)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    private void HandleJoinResponse(IRealtimePush<RealtimeChannel, SocketResponse> sender, SocketResponse message)
    {
        if (message._event != ChannelEventReply) return;

        var obj = JsonSerializer.Deserialize<SocketResponse<PhoenixResponse>>(message.Json!,
            this.Options.SerializerSettings);
        if (obj?.Payload == null) return;

        obj.Payload.Response?.Change?.ForEach(this.BindIdPostgresChanges);

        switch (obj.Payload.Status)
        {
            // A response was received from the channel
            case PhoenixStatusOk:
                // Disable Rejoin Timeout
                this.rejoinTimer.Stop();
                this.isRejoining = false;

                var authPush = this.GenerateAuthPush();
                authPush?.Send();

                // If postgres_changes options are specified, we need to wait for a system event
                // that registers a successful subscription (see HandleSocketMessage.System)
                if (this.PostgresChangesOptions.Count == 0)
                    this.NotifyStateChanged(ChannelState.Joined);
                break;
            case PhoenixStatusError:
                this.rejoinTimer.Stop();
                this.isRejoining = false;

                this.NotifyErrorOccurred(new RealtimeException(message.Json)
                { Reason = FailureHint.Reason.ChannelJoinFailure });
                break;
        }
    }

    /// <summary>
    /// Called when a socket message is received, parses the correct event handler to pass to.
    /// </summary>
    /// <param name="message"></param>
    internal void HandleSocketMessage(SocketResponse message)
    {
        if (message.Ref == this.JoinPush?.Ref) return;

        // If we don't ignore this event we'll end up with double callbacks.
        if (message._event == "*") return;

        this.NotifyMessageReceived(message);

        switch (message.Event)
        {
            // If a channel is subscribed to postgres changes then we have a special case to account for:
            // A system event is emitted after the normal join ACK that says:
            // {"event":"system","payload":{"channel":"public:todos","extension":"postgres_changes","message":"Subscribed to PostgreSQL","status":"ok"}}
            // This switch case emits the join event after this has been received.
            case EventType.System:
                if (!this.IsJoining) return;

                var obj = JsonSerializer.Deserialize<SocketResponse<PhoenixResponse>>(message.Json!,
                    this.Options.SerializerSettings);

                if (obj?.Payload == null) return;

                switch (obj.Payload.Status)
                {
                    case PhoenixStatusOk:
                        this.NotifyStateChanged(ChannelState.Joined);
                        break;
                    case PhoenixStatusError:
                        this.NotifyErrorOccurred(new RealtimeException(message.Json)
                        { Reason = FailureHint.Reason.ChannelJoinFailure });
                        break;
                }

                break;
            // Handles Insert, Update, Delete
            case EventType.PostgresChanges:
                var deserialized =
                    JsonSerializer.Deserialize<PostgresChangesResponse>(message.Json!,
                        this.Options.SerializerSettings);

                if (deserialized?.Payload?.Data == null) return;

                deserialized.Json = message.Json;
                deserialized.SerializerSettings = this.Options.SerializerSettings;
                deserialized.PostgrestClient = this.Options.ClientOptions.PostgrestClient;

                // Invoke '*' listener
                this.NotifyPostgresChanges(deserialized.Payload!.Data!.Type, deserialized);

                break;
            case EventType.Broadcast:
                this.BroadcastHandler?.Invoke(this, message);
                break;
            case EventType.PresenceState:
                this.PresenceSync?.Invoke(this, message);
                break;
            case EventType.PresenceDiff:
                this.PresenceDiff?.Invoke(this, message);
                break;
        }
    }

    /// <summary>
    /// Create a Binding and add to a list
    /// </summary>
    /// <param name="options"></param>
    private void BindPostgresChangesOptions(PostgresChangesOptions options)
    {
        var founded = this.bindings.FirstOrDefault(b => options.Equals(b.Options));
        if (founded != null) return;

        this.bindings.Add(
            new Binding
            {
                Options = options,
            }
        );
    }

    /// <summary>
    /// Try to bind a PostgresChangesHandler to a PostgresChangesOptions
    /// </summary>
    /// <param name="listenType"></param>
    /// <param name="handler"></param>
    private void BindPostgresChangesHandler(ListenType listenType, PostgresChangesHandler handler)
    {
        var founded = this.bindings.FirstOrDefault(b =>
            b.Options?.Event == Core.Helpers.GetMappedToAttr(listenType).Mapping &&
            b.Handler == null
        );
        if (founded != null)
        {
            founded.Handler = handler;
            founded.ListenType = listenType;
            return;
        }

        this.BindPostgresChangesHandlerGeneric(listenType, handler);

    }

    private void BindPostgresChangesHandlerGeneric(ListenType listenType, PostgresChangesHandler handler)
    {
        var founded = this.bindings.FirstOrDefault(b =>
            (b.Options?.Event == Core.Helpers.GetMappedToAttr(listenType).Mapping || b.Options?.Event == "*") &&
            b.Handler == null
        );
        if (founded == null) return;

        founded.Handler = handler;
        founded.ListenType = listenType;
    }

    /// <summary>
    /// Filter the binding list and try to add an id from socket to its binding
    /// </summary>
    /// <param name="joinResponse"></param>
    private void BindIdPostgresChanges(PhoenixPostgresChangeResponse joinResponse)
    {
        var founded = this.bindings.FirstOrDefault(b => b.Options != null &&
                                                    b.Options.Event == joinResponse.EventName &&
                                                    b.Options.Table == joinResponse.Table &&
                                                    b.Options.Schema == joinResponse.Schema &&
                                                    b.Options.Filter == joinResponse.Filter);
        if (founded == null) return;
        founded.Id = joinResponse?.Id;
    }

    /// <summary>
    /// Try to invoke the handler properly based on event type and socket response
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="response"></param>
    private void InvokeProperlyHandlerFromBind(ListenType eventType, PostgresChangesResponse response)
    {
        var all = this.bindings.FirstOrDefault(b =>
        {
            if (b.Options == null && response.Payload == null && b.Handler == null) return false;

            return response.Payload != null && response.Payload.Ids.Contains(b.Id) && eventType != ListenType.All &&
                   b.ListenType == ListenType.All;
        });

        if (all != null)
        {
            all.Handler?.Invoke(this, response);
            return;
        }

        // Invoke all specific handler if possible
        this.bindings.ForEach(binding =>
        {
            if (binding.ListenType != eventType) return;
            if (binding.Options == null || response.Payload == null || binding.Handler == null) return;

            if (response.Payload.Ids.Contains(binding.Id)) binding.Handler.Invoke(this, response);
        });
    }

    /// <summary>
    /// Remove handler from binding
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="handler"></param>
    private void RemovePostgresChangesFromBinding(ListenType eventType, PostgresChangesHandler handler)
    {
        var binding = this.bindings.FirstOrDefault(b => b.Handler == handler && b.ListenType == eventType);
        if (binding == null) return;
        this.bindings.Remove(binding);
    }
}
