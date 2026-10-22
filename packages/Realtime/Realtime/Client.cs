using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;
using Websocket.Client.Exceptions;
using static Supabase.Realtime.Constants;

#pragma warning disable CS1570

namespace Supabase.Realtime;

/// <summary>
/// Singleton that represents a Client connection to a Realtime Server.
///
/// It maintains a singular Websocket with asynchronous listeners (RealtimeChannels).
/// </summary>
/// <example>
///     client = Client.Instance
/// </example>
[SuppressMessage("ReSharper", "InvalidXmlDocComment")]
public class Client : IRealtimeClient<RealtimeSocket, RealtimeChannel>
{
    /// <summary>
    /// Exposes all Realtime RealtimeChannel Subscriptions for R/O public consumption 
    /// </summary>
    [PartialCapability("realtime.client.get_channels", "Active channels exposed as a Subscriptions dictionary; no getChannels() method.")]
    public ReadOnlyDictionary<string, RealtimeChannel> Subscriptions => new(this.subscriptions);

    /// <summary>
    /// The backing Socket class.
    ///
    /// Most methods of the Client act as proxies to the Socket class.
    /// </summary>
    public IRealtimeSocket? Socket { get; private set; }

    /// <summary>
    /// Client Options - most of which are regarding Socket connection Options
    /// </summary>
    public ClientOptions Options { get; }

    private Func<Dictionary<string, string>>? getHeaders { get; set; }

    /// <inheritdoc />
    public Func<Dictionary<string, string>>? GetHeaders
    {
        get => this.getHeaders;
        set
        {
            this.getHeaders = value;

            if (this.Socket != null)
                this.Socket.GetHeaders = value;
        }
    }

    /// <summary>
    /// Custom Serializer resolvers and converters that will be used for encoding and decoding Postgrest JSON responses.
    ///
    /// By default, Postgrest seems to use a date format that C# and Newtonsoft do not like, so this initial
    /// configuration handles that.
    /// </summary>
    public JsonSerializerOptions SerializerSettings => RealtimeSerializerOptions.Build(this.Options);

    /// <summary>
    /// JWT Access token for WALRUS security
    /// </summary>
    private string? AccessToken { get; set; }

    private readonly string realtimeUrl;

    /// <summary>
    /// Handlers for notifications of state changes.
    /// </summary>
    private readonly List<IRealtimeClient<RealtimeSocket, RealtimeChannel>.SocketStateEventHandler>
        socketEventHandlers = new();

    /// <summary>
    /// Contains all Realtime RealtimeChannel Subscriptions - state managed internally.
    ///
    /// Keys are of encoded value: `{database}{:schema?}{:table?}{:col.eq.:value?}`
    /// Values are of type `RealtimeChannel<T> where T : BaseModel, new()`;
    /// </summary>
    private readonly Dictionary<string, RealtimeChannel> subscriptions;

    /// <summary>
    /// Initializes a Client instance, this method should be called prior to any other method.
    /// </summary>
    /// <param name="realtimeUrl">The connection url (ex: "ws://localhost:4000/socket" - no trailing slash required)</param>
    /// <param name="options"></param>
    /// <returns>Client</returns>
    public Client(string realtimeUrl, ClientOptions? options = null)
    {
        this.realtimeUrl = realtimeUrl;
        this.subscriptions = new Dictionary<string, RealtimeChannel>();

        options ??= new ClientOptions();
        options.Encode ??= this.DefaultMessageEncoder;
        options.Decode ??= this.DefaultMessageDecoder;
        this.Options = options;
    }


    /// <summary>
    /// Attempts to connect to the Socket.
    ///
    /// Returns when socket has successfully connected.
    /// </summary>
    /// <returns></returns>
    [ImplementedCapability("realtime.client.connect")]
    public async Task<IRealtimeClient<RealtimeSocket, RealtimeChannel>> ConnectAsync()
    {
        var tsc = new TaskCompletionSource<IRealtimeClient<RealtimeSocket, RealtimeChannel>>();

        if (this.Socket != null)
        {
            Debugger.Instance.Log(this, "Calling `ConnectAsync` on an instance that already has a `Socket`");
            tsc.SetResult(this);
        }

        this.Socket = new RealtimeSocket(this.realtimeUrl, this.Options);
        this.Socket.GetHeaders = this.GetHeaders;

        IRealtimeSocket.StateEventHandler? socketStateHandler = null;
        socketStateHandler = (sender, state) =>
        {
            if (state != SocketState.Open) return;

            sender.RemoveStateChangedHandler(socketStateHandler!);

            this.Socket.AddMessageReceivedHandler(this.HandleSocketMessageReceived);
            this.Socket.AddHeartbeatHandler(this.HandleSocketHeartbeat);

            this.NotifySocketStateChange(SocketState.Open);
            tsc.TrySetResult(this);
        };

        this.Socket.AddStateChangedHandler(socketStateHandler);

        try
        {
            await this.Socket.Connect();
            await tsc.Task;
        }
        catch (WebsocketException ex)
        {
            this.Socket = null;
            throw new RealtimeException(ex.Message, ex) { Reason = FailureHint.Reason.SocketError };
        }

        return this;
    }

    /// <summary>
    /// Attempts to connect to the socket.
    ///
    /// Provides a callback for `Task` driven returns.
    /// </summary>
    /// <param name="callback"></param>
    /// <returns></returns>
    [Obsolete("Please use ConnectAsync() instead.")]
    [ImplementedCapability("realtime.client.connect")]
    public IRealtimeClient<RealtimeSocket, RealtimeChannel> Connect(
        Action<IRealtimeClient<RealtimeSocket, RealtimeChannel>, RealtimeException?>? callback = null)
    {
        if (this.Socket != null)
        {
            Debugger.Instance.Log(this, "Calling `ConnectAsync` on an instance that already has a `Socket`");
            callback?.Invoke(this, null);
            return this;
        }

        this.Socket = new RealtimeSocket(this.realtimeUrl, this.Options);
        IRealtimeSocket.StateEventHandler? socketStateHandler = null;
        IRealtimeSocket.ErrorEventHandler? errorEventHandler = null;

        socketStateHandler = (sender, state) =>
        {
            if (state != SocketState.Open) return;

            this.Socket.AddMessageReceivedHandler(this.HandleSocketMessageReceived);
            this.Socket.AddHeartbeatHandler(this.HandleSocketHeartbeat);

            sender.RemoveStateChangedHandler(socketStateHandler!);
            sender.RemoveErrorHandler(errorEventHandler!);

            this.NotifySocketStateChange(SocketState.Open);

            callback?.Invoke(this, null);
        };

        errorEventHandler = (sender, ex) =>
        {
            this.Socket = null;
            callback?.Invoke(this, ex);
        };

        this.Socket.AddStateChangedHandler(socketStateHandler);
        this.Socket.AddErrorHandler(errorEventHandler);
        this.Socket.Connect();

        return this;
    }

    /// <summary>
    /// Adds a listener to be notified when the socket state changes.
    /// </summary>
    [PartialCapability("realtime.client.connection_state", "Socket state delivered via SocketStateEventHandler events; no synchronous connection-state getter on the client.")]
    public void AddStateChangedHandler(
        IRealtimeClient<RealtimeSocket, RealtimeChannel>.SocketStateEventHandler handler)
    {
        if (this.socketEventHandlers.Contains(handler))
            return;

        this.socketEventHandlers.Add(handler);
    }

    /// <summary>
    /// Removes a specified listener from socket state changes.
    /// </summary>
    public void RemoveStateChangedHandler(
        IRealtimeClient<RealtimeSocket, RealtimeChannel>.SocketStateEventHandler handler)
    {
        if (!this.socketEventHandlers.Contains(handler))
            return;

        this.socketEventHandlers.Remove(handler);
    }

    /// <summary>
    /// Clears all of the listeners from receiving socket state changes.
    /// </summary>
    public void ClearStateChangedHandlers() =>
        this.socketEventHandlers.Clear();

    /// <summary>
    /// Notifies all listeners that the current user auth state has changed.
    ///
    /// This is mainly used internally to fire notifications - most client applications won't need this.
    /// </summary>
    /// <param name="stateChanged"></param>
    private void NotifySocketStateChange(SocketState stateChanged)
    {
        foreach (var handler in this.socketEventHandlers.ToArray())
            handler.Invoke(this, stateChanged);
    }

    /// <summary>
    /// Adds a debug handler, likely used within a logging solution of some kind.
    /// </summary>
    /// <param name="handler"></param>
    [PartialCapability("realtime.configuration.custom_logger", "Diagnostics routed through a debug handler hook; no custom logging function with a log-level filter.")]
    public void AddDebugHandler(IRealtimeDebugger.DebugEventHandler handler) =>
        Debugger.Instance.AddDebugHandler(handler);


    /// <summary>
    /// Removes a debug handler
    /// </summary>
    /// <param name="handler"></param>
    public void RemoveDebugHandler(IRealtimeDebugger.DebugEventHandler handler) =>
        Debugger.Instance.RemoveDebugHandler(handler);

    /// <summary>
    /// Clears debug handlers;
    /// </summary>
    public void ClearDebugHandlers() => Debugger.Instance.ClearDebugHandlers();


    /// <summary>
    /// Sets the current Access Token every heartbeat (see: https://github.com/supabase/realtime-js/blob/59bd47956ebe4e23b3e1a6c07f5fe2cfe943e8ad/src/RealtimeClient.ts#L437)
    /// </summary>
    private void HandleSocketHeartbeat(IRealtimeSocket sender, SocketResponse message)
    {
        if (!string.IsNullOrEmpty(this.AccessToken))
            this.SetAuth(this.AccessToken!);
    }

    /// <summary>
    /// Disconnects from the socket server (if connected).
    /// </summary>
    /// <param name="code">Status Code</param>
    /// <param name="reason">Reason for disconnect</param>
    /// <returns></returns>
    [ImplementedCapability("realtime.client.disconnect")]
    public IRealtimeClient<RealtimeSocket, RealtimeChannel> Disconnect(
        WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "Programmatic Disconnect")
    {
        if (this.Socket != null)
        {
            this.Socket.RemoveMessageReceivedHandler(this.HandleSocketMessageReceived);
            this.Socket.RemoveStateChangedHandler(this.HandleSocketStateChanged);
            this.Socket.Disconnect(code, reason);
            this.Socket = null;
        }

        return this;
    }

    /// <summary>
    /// Sets the JWT access token used for channel subscription authorization and Realtime RLS.
    /// Ref: https://github.com/supabase/realtime-js/pull/117 | https://github.com/supabase/realtime-js/pull/117
    /// </summary>
    /// <param name="jwt"></param>
    [ImplementedCapability("realtime.client.set_auth_token")]
    public void SetAuth(string jwt)
    {
        this.AccessToken = jwt;

        foreach (var channel in this.subscriptions.Values)
        {
            // See: https://github.com/supabase/realtime-js/pull/126
            channel.Options.Parameters!["user_token"] = this.AccessToken;

            if (channel.HasJoinedOnce && channel.IsJoined)
            {
                channel.Push(Constants.ChannelAccessToken, payload: new Dictionary<string, string>
                {
                    { "access_token", this.AccessToken }
                });
            }
        }
    }

    /// <summary>
    /// Adds a RealtimeChannel subscription - if a subscription exists with the same signature, the existing subscription will be returned.
    /// </summary>
    /// <param name="channelName">
    /// The name of the Channel to join (totally arbitrary). The topic is namespaced under a
    /// <c>realtime:</c> prefix; a name that already begins with <c>realtime:</c> is used as-is
    /// rather than prefixed again.
    /// </param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [ImplementedCapability("realtime.client.channel", "Plain-topic form plus a typed convenience overload Channel(db, schema, table, col, value).")]
    public RealtimeChannel Channel(string channelName) =>
        this.Channel(channelName, ChannelOptions.Public(this.Options, () => this.AccessToken, this.SerializerSettings));

    /// <summary>
    /// Adds a RealtimeChannel subscription with custom options - if a subscription exists with the same signature, the existing subscription will be returned.
    /// </summary>
    /// <param name="channelName">
    /// The name of the Channel to join. The topic is namespaced under a <c>realtime:</c> prefix; a name
    /// that already begins with <c>realtime:</c> is used as-is rather than prefixed again.
    /// </param>
    /// <param name="options">Custom channel options for configuring the subscription</param>
    /// <returns>A RealtimeChannel instance representing the subscription</returns>
    /// <exception cref="Exception">Thrown when Socket is null, indicating Connect() was not called</exception>
    public RealtimeChannel Channel(string channelName, ChannelOptions options)
    {
        var topic = channelName.StartsWith("realtime:", StringComparison.Ordinal)
            ? channelName
            : $"realtime:{channelName}";

        if (this.subscriptions.TryGetValue(topic, out var channel))
            return channel;

        if (this.Socket == null)
            throw new Exception("Socket must exist, was `Connect` called?");

        var subscription = new RealtimeChannel(this.Socket!, topic, options);
        subscription.AddStateChangedHandler(this.HandleChannelStateChanged);
        this.subscriptions.Add(topic, subscription);

        return subscription;
    }

    /// <summary>
    /// Adds a RealtimeChannel subscription - if a subscription exists with the same signature, the existing subscription will be returned.
    /// </summary>
    /// <param name="database">Database to connect to, with Supabase this will likely be `realtime`.</param>
    /// <param name="schema">Postgres schema, usually `public`</param>
    /// <param name="table">Postgres table name</param>
    /// <param name="column">Postgres column name</param>
    /// <param name="value">Value the specified column should have</param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public RealtimeChannel Channel(string database = "realtime", string schema = "public", string table = "*",
        string? column = null, string? value = null, Dictionary<string, string>? parameters = null)
    {
        var key = Utils.GenerateChannelTopic(database, schema, table, column, value);

        if (this.subscriptions.TryGetValue(key, out var channel))
            return channel;

        if (this.Socket == null)
            throw new Exception("Socket must exist, was `Connect` called?");

        var changesOptions = new PostgresChangesOptions(schema, table,
            filter: column != null && value != null ? $"{column}=eq.{value}" : null, parameters: parameters);
        var options = ChannelOptions.Public(this.Options, () => this.AccessToken, this.SerializerSettings);

        var subscription = new RealtimeChannel(this.Socket!, key, options);
        subscription.RegisterPostgresChangesOptions(changesOptions);
        subscription.AddStateChangedHandler(this.HandleChannelStateChanged);

        this.subscriptions.Add(key, subscription);

        return subscription;
    }

    // Forget a closed channel so Channel(name) returns a fresh one. If the old one joins again, it comes back.
    private void HandleChannelStateChanged(IRealtimeChannel sender, ChannelState state)
    {
        var channel = (RealtimeChannel) sender;
        if (state == ChannelState.Closed && this.subscriptions.TryGetValue(channel.Topic, out var current) && current == channel)
            this.subscriptions.Remove(channel.Topic);
        else if (state == ChannelState.Joining)
            this.subscriptions.TryAdd(channel.Topic, channel);
    }

    /// <summary>
    /// Removes a channel subscription.
    /// </summary>
    /// <param name="channel"></param>
    [ImplementedCapability("realtime.client.remove_channel")]
    public void Remove(RealtimeChannel channel)
    {
        if (this.subscriptions.ContainsKey(channel.Topic))
        {
            if (channel.IsJoined)
                channel.Unsubscribe();

            this.subscriptions.Remove(channel.Topic);
        }
    }

    /// <summary>
    /// The default socket message encoder, used to serialize <see cref="JoinPush"/> messages to the socket
    /// server.
    ///
    /// It is unlikely that this will be overriden by the developer.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="callback"></param>
    private void DefaultMessageEncoder(object payload, Action<string> callback) => callback(JsonSerializer.Serialize(payload, payload.GetType(), this.SerializerSettings));

    /// <summary>
    /// The default socket message decoder, used to deserialize messages from the socket server.
    /// Ref: <see cref="SocketResponse{T}"/>
    ///
    /// It is unlikely that this will be overriden by the developer. 
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="callback"></param>
    private void DefaultMessageDecoder(string payload, Action<SocketResponse?> callback)
    {
        var response = JsonSerializer.Deserialize<SocketResponse>(payload, this.SerializerSettings);
        if (response != null)
            response.SerializerSettings = this.SerializerSettings;
        callback(response);
    }

    private void HandleSocketMessageReceived(IRealtimeSocket sender, SocketResponse message)
    {
        if (message.Topic != null && this.subscriptions.TryGetValue(message.Topic, out var subscription))
            subscription.HandleSocketMessage(message);
    }

    private void HandleSocketStateChanged(IRealtimeSocket sender, SocketState state)
    {
        switch (state)
        {
            case SocketState.Open:
                // Ref: https://github.com/supabase/realtime-js/pull/116/files
                if (!string.IsNullOrEmpty(this.AccessToken))
                    this.SetAuth(this.AccessToken!);

                this.NotifySocketStateChange(SocketState.Open);
                break;
            case SocketState.Reconnect:
                // Ref: https://github.com/supabase/realtime-js/pull/116/files
                if (!string.IsNullOrEmpty(this.AccessToken))
                    this.SetAuth(this.AccessToken!);

                this.NotifySocketStateChange(SocketState.Reconnect);
                break;
            default:
                this.NotifySocketStateChange(state);
                break;
        }
    }
}
