using System;
using System.Collections.Generic;
using System.Timers;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;

namespace Supabase.Realtime.Channel;

/// <summary>
/// Class representation of a single request sent to the Socket server.
///
/// `Push` also adds additional functionality for retrying, timeouts, and listeners
/// for its associated response from the server.
/// </summary>
public class Push : IRealtimePush<RealtimeChannel, SocketResponse>
{
    /// <summary>
    /// Flag representing the `sent` state of a request.
    /// </summary>
    public bool IsSent { get; private set; }

    /// <summary>
    /// Invoked when this `Push` has not been responded to within the timeout interval.
    /// </summary>
    public event EventHandler? OnTimeout;

    /// <summary>
    /// Accessor for the returned Socket Response
    /// </summary>
    public IRealtimeSocketResponse? Response { get; private set; }

    /// <summary>
    /// The associated channel.
    /// </summary>
    public RealtimeChannel Channel { get; }

    private string? Type { get; }

    /// <summary>
    /// The event requested.
    /// </summary>
    public string EventName { get; }

    /// <summary>
    /// Payload of data to be sent.
    /// </summary>
    public object? Payload { get; }

    /// <summary>
    /// Represents the Pushed (sent) Message
    /// </summary>
    public SocketRequest? Message { get; private set; }

    /// <summary>
    /// Ref Of this Message
    /// </summary>
    public string? Ref { get; private set; }

    private string? _msgRefEvent;
    private int _timeoutMs;
    private readonly Timer _timer;

    private readonly IRealtimeSocket _socket;

    /// <summary>
    /// Handlers for notifications of message events.
    /// </summary>
    private readonly List<IRealtimePush<RealtimeChannel, SocketResponse>.MessageEventHandler> _messageEventHandlers = new();

    /// <summary>
    /// Initializes a single request that will be `Pushed` to the Socket server.
    /// </summary>
    /// <param name="socket"></param>
    /// <param name="channel"></param>
    /// <param name="eventName"></param>
    /// <param name="type"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    public Push(IRealtimeSocket socket, RealtimeChannel channel, string eventName, string? type = null,
        object? payload = null, int timeoutMs = Constants.DefaultTimeout)
    {
        this._socket = socket;
        this._timeoutMs = timeoutMs;
        this._timer = new Timer(this._timeoutMs);
        this._timer.Elapsed += this.HandleTimeoutElapsed;

        this.Channel = channel;
        this.Type = type;
        this.EventName = eventName;
        this.Payload = payload;

        socket.AddMessageReceivedHandler(this.HandleSocketMessageReceived);
    }

    /// <summary>
    /// Resends a `Push` request.
    /// </summary>
    /// <param name="timeoutMs"></param>
    public void Resend(int timeoutMs = Constants.DefaultTimeout)
    {
        this._timer.Interval = timeoutMs;
        this.Ref = null;
        this._msgRefEvent = null;

        this.IsSent = false;
        this.Send();
    }

    /// <summary>
    /// Sends a `Push` request and initializes the Timeout.
    /// </summary>
    public void Send()
    {
        this.StartTimeout();
        this.IsSent = true;

        this.Message = new SocketRequest
        {
            Topic = this.Channel.Topic,
            Type = this.Type,
            Event = this.EventName,
            Payload = this.Payload,
            Ref = this.Ref,
            JoinRef = this.Channel.JoinPush?.Ref,
        };

        this._socket.Push(this.Message);
    }

    /// <summary>
    /// Keeps an internal timer for raising an event if this message is not responded to.
    /// </summary>
    internal void StartTimeout()
    {
        this._timer.Stop();
        this._timer.Start();
        this.Ref = this._socket.MakeMsgRef();
        this._msgRefEvent = this._socket.ReplyEventName(this.Ref);
    }

    /// <summary>
    /// Handles when a socket message is received for this push.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    private void HandleSocketMessageReceived(IRealtimeSocket sender, SocketResponse message)
    {
        // Needs to verify if realtime server won't send the message below anymore after receive a track presence event
        // {"ref":"bd07efe5-ca06-4257-b080-79779c6f76c4","event":"phx_reply","payload":{"status":"ok","response":{}},"topic":"realtime:online-users"}
        // the message was used to stop timeout handler
        // All tests still work on version before 2.34.21
        var isPresenceDiff = message is { Event: Constants.EventType.PresenceDiff };
        if (!isPresenceDiff && message.Ref != this.Ref) return;

        this.CancelTimeout();
        this.Response = message;
        this.NotifyMessageReceived(message);

        sender.RemoveMessageReceivedHandler(this.HandleSocketMessageReceived);
    }

    /// <summary>
    /// Adds a listener to be notified when a message is received.
    /// </summary>
    /// <param name="handler"></param>
    public void AddMessageReceivedHandler(IRealtimePush<RealtimeChannel, SocketResponse>.MessageEventHandler handler)
    {
        if (this._messageEventHandlers.Contains(handler))
            return;

        this._messageEventHandlers.Add(handler);
    }

    /// <summary>
    /// Removes a specified listener from messages received.
    /// </summary>
    /// <param name="handler"></param>
    public void RemoveMessageReceivedHandler(IRealtimePush<RealtimeChannel, SocketResponse>.MessageEventHandler handler)
    {
        if (!this._messageEventHandlers.Contains(handler))
            return;

        this._messageEventHandlers.Remove(handler);
    }

    /// <summary>
    /// Notifies all listeners that the socket has received a message
    /// </summary>
    /// <param name="messageResponse"></param>
    private void NotifyMessageReceived(SocketResponse messageResponse)
    {
        foreach (var handler in this._messageEventHandlers.ToArray())
            handler.Invoke(this, messageResponse);
    }

    /// <summary>
    /// Clears all of the listeners from receiving event state changes.
    /// </summary>
    public void ClearMessageReceivedHandler() =>
        this._messageEventHandlers.Clear();

    private void HandleTimeoutElapsed(object sender, ElapsedEventArgs e) => OnTimeout?.Invoke(this, null);

    private void CancelTimeout() => this._timer.Stop();
}
