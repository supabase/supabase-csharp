using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Extensions;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;
using Supabase.Realtime.Sockets;
using Websocket.Client;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime;
/// <summary>
/// Socket connection handler.
/// </summary>
public class RealtimeSocket : IDisposable, IRealtimeSocket
{
    /// <summary>
    /// Returns whether or not the connection is alive.
    /// </summary>
    public bool IsConnected => this.connection.IsRunning;

    /// <summary>
    /// The Socket Endpoint
    /// </summary>
    private string EndpointUrl
    {
        get
        {
            var parameters = new Dictionary<string, string?>
            {
                { "token", this.options.Parameters.Token },
                { "apikey", this.options.Parameters.ApiKey },
                { "vsn", "1.0.0" }
            };

            return string.Format($"{this.endpoint}?{Utils.QueryString(parameters)}");
        }
    }

    /// <inheritdoc />
    public Func<Dictionary<string, string>>? GetHeaders { get; set; }

    /// <summary>
    /// Shortcut property that merges <see cref="GetHeaders"/> with <see cref="options"/>
    /// Headers specified in <see cref="options"/> take precedence over <see cref="GetHeaders"/>
    /// </summary>
    internal Dictionary<string, string> Headers => this.GetHeaders != null ? this.GetHeaders().MergeLeft(this.options.Headers) : this.options.Headers;

    private readonly List<IRealtimeSocket.StateEventHandler> socketEventHandlers = new();
    private readonly List<IRealtimeSocket.MessageEventHandler> messageEventHandlers = new();
    private readonly List<IRealtimeSocket.HeartbeatEventHandler> heartbeatEventHandlers = new();
    private readonly List<IRealtimeSocket.ErrorEventHandler> errorEventHandlers = new();

    private readonly string endpoint;
    private readonly ClientOptions options;
    private readonly IWebsocketClient connection;

    private CancellationTokenSource? heartbeatTokenSource;

    private bool hasSuccessfullyConnectedOnce;
    private bool hasPendingHeartbeat;
    private string? pendingHeartbeatRef;

    private readonly List<Task> buffer = new();
    private bool isReconnecting;
    private int reconnectionAttempts = 0;

    /// <summary>
    /// Initializes this Socket instance.
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="options"></param>
    public RealtimeSocket(string endpoint, ClientOptions options)
    {
        this.endpoint = $"{endpoint}/{TransportWebsocket}";
        this.options = options;

        if (!options.Headers.ContainsKey("X-Client-Info"))
            options.Headers.Add("X-Client-Info", Core.Util.GetAssemblyVersion(typeof(Client)));

        this.connection = (options.WebSocketFactory ?? DefaultWebSocketFactory.Instance)
            .Create(new Uri(this.EndpointUrl), () => this.Headers);
    }

    void IDisposable.Dispose()
    {
        this.heartbeatTokenSource?.Cancel();
        this.DisposeConnection();
    }

    /// <summary>
    /// Connects to a socket server and registers event listeners.
    /// </summary>
    public async Task Connect()
    {
        if (this.connection.IsRunning) return;

        this.connection.ReconnectTimeout = this.options.ReconnectAfterInterval(this.reconnectionAttempts);
        this.connection.ErrorReconnectTimeout = TimeSpan.FromSeconds(30);

        this.connection.ReconnectionHappened.Subscribe(this.HandleSocketReconnectionHappened);
        this.connection.DisconnectionHappened.Subscribe(this.HandleSocketDisconnectionHappened);
        this.connection.MessageReceived.Subscribe(this.HandleSocketMessage);

        await this.connection.StartOrFail();
    }

    /// <summary>
    /// Disconnects from the socket server.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="reason"></param>
    public void Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "")
    {
        this.heartbeatTokenSource?.Cancel();
        this.connection.Stop(code, reason);
    }

    #region Event Listeners

    /// <summary>
    /// Adds a listener to be notified when the socket state changes.
    /// </summary>
    /// <param name="handler"></param>
    public void AddStateChangedHandler(IRealtimeSocket.StateEventHandler handler)
    {
        if (!this.socketEventHandlers.Contains(handler))
            this.socketEventHandlers.Add(handler);
    }

    /// <summary>
    /// Removes a specified listener from socket state changes.
    /// </summary>
    /// <param name="handler"></param>
    public void RemoveStateChangedHandler(IRealtimeSocket.StateEventHandler handler)
    {
        if (this.socketEventHandlers.Contains(handler))
            this.socketEventHandlers.Remove(handler);
    }

    /// <summary>
    /// Notifies all listeners that the socket state has changed.
    /// </summary>
    /// <param name="newState"></param>
    private void NotifySocketStateChange(SocketState newState)
    {
        if (!this.socketEventHandlers.Any()) return;

        Debugger.Instance.Log(this, $"Socket State Change: {newState}");

        foreach (var handler in this.socketEventHandlers.ToArray())
            handler.Invoke(this, newState);
    }

    /// <summary>
    /// Clears all of the listeners from receiving event state changes.
    /// </summary>
    public void ClearStateChangedHandlers() =>
        this.socketEventHandlers.Clear();

    /// <summary>
    /// Adds a listener to be notified when a message is received.
    /// </summary>
    /// <param name="handler"></param>
    public void AddMessageReceivedHandler(IRealtimeSocket.MessageEventHandler handler)
    {
        if (this.messageEventHandlers.Contains(handler))
            return;

        this.messageEventHandlers.Add(handler);
    }

    /// <summary>
    /// Removes a specified listener from messages received.
    /// </summary>
    /// <param name="handler"></param>
    public void RemoveMessageReceivedHandler(IRealtimeSocket.MessageEventHandler handler)
    {
        if (!this.messageEventHandlers.Contains(handler))
            return;

        this.messageEventHandlers.Remove(handler);
    }

    /// <summary>
    /// Notifies all listeners that the socket has received a message
    /// </summary>
    /// <param name="heartbeat"></param>
    private void NotifyMessageReceived(SocketResponse heartbeat)
    {
        foreach (var handler in this.messageEventHandlers.ToArray())
            handler.Invoke(this, heartbeat);
    }

    /// <summary>
    /// Clears all of the listeners from receiving event state changes.
    /// </summary>
    public void ClearMessageReceivedHandlers() =>
        this.messageEventHandlers.Clear();

    /// <summary>
    /// Adds a listener to be notified when a message is received.
    /// </summary>
    /// <param name="handler"></param>
    public void AddHeartbeatHandler(IRealtimeSocket.HeartbeatEventHandler handler)
    {
        if (!this.heartbeatEventHandlers.Contains(handler))
            this.heartbeatEventHandlers.Add(handler);
    }

    /// <summary>
    /// Removes a specified listener from messages received.
    /// </summary>
    /// <param name="handler"></param>
    public void RemoveHeartbeatHandler(IRealtimeSocket.HeartbeatEventHandler handler)
    {
        if (this.heartbeatEventHandlers.Contains(handler))
            this.heartbeatEventHandlers.Remove(handler);
    }

    /// <summary>
    /// Notifies all listeners that the socket has received a heartbeat
    /// </summary>
    /// <param name="heartbeat"></param>
    private void NotifyHeartbeatReceived(SocketResponse heartbeat)
    {
        foreach (var handler in this.heartbeatEventHandlers.ToArray())
            handler.Invoke(this, heartbeat);
    }

    /// <summary>
    /// Clears all of the listeners from receiving event state changes.
    /// </summary>
    public void ClearHeartbeatHandlers() =>
        this.heartbeatEventHandlers.Clear();

    /// <summary>
    /// Adds an error event handler.
    /// </summary>
    /// <param name="handler"></param>
    public void AddErrorHandler(IRealtimeSocket.ErrorEventHandler handler)
    {
        if (!this.errorEventHandlers.Contains(handler))
            this.errorEventHandlers.Add(handler);
    }

    /// <summary>
    /// Removes an error event handler
    /// </summary>
    /// <param name="handler"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void RemoveErrorHandler(IRealtimeSocket.ErrorEventHandler handler)
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
        this.NotifySocketStateChange(SocketState.Error);

        foreach (var handler in this.errorEventHandlers.ToArray())
            handler.Invoke(this, exception);
    }

    #endregion

    /// <summary>
    /// Pushes formatted data to the socket server.
    ///
    /// If the connection is not alive, the data will be placed into a buffer to be sent when reconnected.
    /// </summary>
    /// <param name="data"></param>
    public void Push(SocketRequest data)
    {
        Debugger.Instance.Log(this,
            $"Socket Push [topic: {data.Topic}, event: {data.Event}, ref: {data.Ref}]:\n\t{JsonSerializer.Serialize(data.Payload, new JsonSerializerOptions { WriteIndented = true })}");

        var task = new Task(() => this.options.Encode!(data, encoded => this.connection.Send(encoded)));

        if (this.connection.IsRunning)
            task.Start();
        else
            this.buffer.Add(task);
    }

    /// <summary>
    /// Returns the latency (in millis) of roundtrip time from socket to server and back.
    /// </summary>
    /// <returns></returns>
    public Task<double> GetLatency()
    {
        var tsc = new TaskCompletionSource<double>();
        var start = DateTime.Now;
        var pingRef = Guid.NewGuid().ToString();

        // ReSharper disable once ConvertToLocalFunction
        IRealtimeSocket.MessageEventHandler? messageHandler = null;
        messageHandler = (_, messageResponse) =>
        {
            if (messageResponse.Ref != pingRef) return;

            this.RemoveMessageReceivedHandler(messageHandler!);
            tsc.SetResult((DateTime.Now - start).TotalMilliseconds);
        };
        this.AddMessageReceivedHandler(messageHandler);

        this.Push(new SocketRequest { Topic = "phoenix", Event = "heartbeat", Ref = pingRef });

        return tsc.Task;
    }

    /// <summary>
    /// Maintains a heartbeat connection with the socket server to prevent disconnection.
    /// </summary>
    private void SendHeartbeat()
    {
        if (!this.connection.IsRunning) return;

        if (this.hasPendingHeartbeat)
        {
            this.hasPendingHeartbeat = false;
            Debugger.Instance.Log(this, "Socket Heartbeat Timeout: Attempting to re-establish a connection.");
            this.connection.Stop(WebSocketCloseStatus.EndpointUnavailable, "heartbeat timeout");
            return;
        }

        this.pendingHeartbeatRef = this.MakeMsgRef();

        this.Push(new SocketRequest
        {
            Topic = "phoenix", Event = "heartbeat", Ref = this.pendingHeartbeatRef,
            Payload = new Dictionary<string, string>()
        });
    }

    /// <summary>
    /// Called when the socket opens, registers the heartbeat thread and cancels the reconnection timer.
    /// </summary>
    private void HandleSocketOpened()
    {
        this.reconnectionAttempts = 0;
        this.hasSuccessfullyConnectedOnce = true;

        // Was a reconnection attempt
        if (this.isReconnecting)
            this.NotifySocketStateChange(SocketState.Reconnect);

        // Reset flag for reconnections
        this.isReconnecting = false;

        Debugger.Instance.Log(this, $"Socket Connected to: {this.EndpointUrl}");

        this.heartbeatTokenSource?.Cancel();
        this.hasPendingHeartbeat = false;
        this.heartbeatTokenSource = new CancellationTokenSource();
        Task.Run(this.EmitHeartbeat, this.heartbeatTokenSource.Token);

        // Send any pending `Push` messages that were queued while socket was disconnected.
        this.FlushBuffer();

        this.NotifySocketStateChange(SocketState.Open);
    }

    private async Task EmitHeartbeat()
    {
        while (this.heartbeatTokenSource is { IsCancellationRequested: false })
        {
            this.SendHeartbeat();
            await Task.Delay(this.options.HeartbeatInterval, this.heartbeatTokenSource.Token);
        }
    }

    #region Socket Event Handlers

    /// <summary>
    /// The socket has reconnected (or connected)
    /// </summary>
    /// <param name="reconnectionInfo"></param>
    private void HandleSocketReconnectionHappened(ReconnectionInfo reconnectionInfo)
    {
        Debugger.Instance.Log(this, $"Socket Reconnection: {reconnectionInfo.Type}");

        if (reconnectionInfo.Type != ReconnectionType.Initial)
            this.isReconnecting = true;

        this.HandleSocketOpened();
    }

    /// <summary>
    /// The socket has disconnected, called either through a socket closing or erroring.
    /// </summary>
    /// <param name="disconnectionInfo"></param>
    private void HandleSocketDisconnectionHappened(DisconnectionInfo disconnectionInfo)
    {
        Debugger.Instance.Log(this, $"Socket Disconnection: {disconnectionInfo.Type}", disconnectionInfo.Exception);

        if (disconnectionInfo.Exception != null)
            this.HandleSocketError(disconnectionInfo);
        else
            this.HandleSocketClosed(disconnectionInfo);
    }

    /// <summary>
    /// Parses a received socket message into a non-generic type.
    /// </summary>
    /// <param name="args"></param>
    private void HandleSocketMessage(ResponseMessage args)
    {
        this.options.Decode!(args.Text, decoded =>
        {
            Debugger.Instance.Log(this, $"Socket Message Received:\n\t{args.Text}");

            // Send Separate heartbeat event
            if (decoded!.Ref == this.pendingHeartbeatRef)
            {
                this.NotifyHeartbeatReceived(decoded);
                return;
            }

            decoded.Json = args.Text;
            this.NotifyMessageReceived(decoded);
        });
    }

    /// <summary>
    /// Handles socket errors, increments reconnection count if a connection has been established at least once.
    /// </summary>
    /// <param name="disconnectionInfo"></param>
    /// <exception cref="Exception"></exception>
    private void HandleSocketError(DisconnectionInfo? disconnectionInfo = null)
    {
        switch (this.hasSuccessfullyConnectedOnce)
        {
            case true:
                {
                    this.isReconnecting = true;
                    this.connection.ReconnectTimeout = this.options.ReconnectAfterInterval(++this.reconnectionAttempts);
                    var nextInterval = DateTime.Now.AddSeconds(this.connection.ReconnectTimeout.Value.Seconds).ToLocalTime();
                    Debugger.Instance.Log(this, $"Next reconnection attempt will occur at: {nextInterval}");
                    break;
                }
            case false when disconnectionInfo is { Exception: not RealtimeException }:
                this.NotifyErrorOccurred(RealtimeException.FromDisconnectionInfo(disconnectionInfo));
                break;
        }
    }

    /// <summary>
    /// Begins the reconnection thread with a progressively increasing interval.
    /// </summary>
    private void HandleSocketClosed(DisconnectionInfo? disconnectionInfo = null) => Debugger.Instance.Log(this, $"Socket Closed at {DateTime.Now.ToLocalTime()}", disconnectionInfo?.Exception);

    #endregion

    /// <summary>
    /// Generates an incrementing identifier for message references - this reference is used
    /// to coordinate requests with their responses.
    /// </summary>
    /// <returns></returns>
    public string MakeMsgRef() => Guid.NewGuid().ToString();

    /// <summary>
    /// Returns the expected reply event name based off a generated message ref.
    /// </summary>
    /// <param name="msgRef"></param>
    /// <returns></returns>
    public string ReplyEventName(string msgRef) => $"chan_reply_{msgRef}";

    /// <summary>
    /// Dispose of the web socket connection.
    /// </summary>
    private async void DisposeConnection()
    {
        await this.connection.Stop(WebSocketCloseStatus.NormalClosure, string.Empty);
        this.connection.Dispose();
    }

    /// <summary>
    /// Flushes `Push` requests added while a socket was disconnected.
    /// </summary>
    private void FlushBuffer()
    {
        if (!this.connection.IsRunning || this.buffer.Count == 0) return;

        Debugger.Instance.Log(this,
            $"Socket Flushing Buffer: Connection has been reestablished and socket is sending {this.buffer.Count} messages");
        foreach (var item in this.buffer)
        {
            item.Start();
        }

        this.buffer.Clear();
    }
}
