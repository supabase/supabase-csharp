using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Presence.Responses;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime;

/// <summary>
/// Represents a realtime presence client.
/// 
/// When a client subscribes to a channel, it will immediately receive the channel's latest state in a single message.
/// Clients are free to come-and-go as they please, and as long as they are all subscribed to the same channel then they will all have the same Presence state as each other.
/// If a client is suddenly disconnected (for example, they go offline), their state will be automatically removed from the shared state.
/// </summary>
/// <typeparam name="TPresenceModel">A model representing expected payload.</typeparam>
[PartialCapability("realtime.subscriptions.subscribe_presence", "Join/leave/sync handlers exist, but the presence sync diverges from JS (see realtime.presence.presence_state).")]
public class RealtimePresence<TPresenceModel> : IRealtimePresence where TPresenceModel : BasePresence
{
    /// <summary>
    /// The Last State of this Presence instance.
    /// </summary>
    [PartialCapability("realtime.presence.presence_state", "Typed CurrentState/LastState exist, but the initial presence snapshot is dropped, so any client that joins after others undercounts everyone — a behavioral gap, not just a shape gap.")]
    public Dictionary<string, List<TPresenceModel>> LastState { get; private set; } =
        new();

    /// <summary>
    /// The Current State of this Presence instance.
    /// </summary>
    [PartialCapability("realtime.presence.presence_state", "Typed CurrentState/LastState exist, but the initial presence snapshot is dropped, so any client that joins after others undercounts everyone — a behavioral gap, not just a shape gap.")]
    public Dictionary<string, List<TPresenceModel>> CurrentState { get; } = new();

    private PresenceOptions options;
    private SocketResponse? currentResponse;
    private readonly RealtimeChannel channel;
    private readonly JsonSerializerOptions serializerSettings;

    private readonly Dictionary<IRealtimePresence.EventType, List<IRealtimePresence.PresenceEventHandler>>
        presenceEventListeners = new();

    /// <summary>
    /// Initializes a realtime presence helper class.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="options"></param>
    /// <param name="serializerSettings"></param>
    public RealtimePresence(RealtimeChannel channel, PresenceOptions options,
        JsonSerializerOptions serializerSettings)
    {
        this.channel = channel;
        this.options = options;
        this.serializerSettings = serializerSettings;
    }

    /// <summary>
    /// Add presence event handler for a given event type.
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="handler"></param>
    public void AddPresenceEventHandler(IRealtimePresence.EventType eventType,
        IRealtimePresence.PresenceEventHandler handler)
    {
        if (!this.presenceEventListeners.ContainsKey(eventType))
            this.presenceEventListeners[eventType] = new List<IRealtimePresence.PresenceEventHandler>();

        if (!this.presenceEventListeners[eventType].Contains(handler))
            this.presenceEventListeners[eventType].Add(handler);
    }

    /// <summary>
    /// Remove an event handler
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="handler"></param>
    public void RemovePresenceEventHandlers(IRealtimePresence.EventType eventType,
        IRealtimePresence.PresenceEventHandler handler)
    {
        if (this.presenceEventListeners.ContainsKey(eventType) &&
            this.presenceEventListeners[eventType].Contains(handler))
            this.presenceEventListeners[eventType].Remove(handler);
    }

    /// <summary>
    /// Clears all event handlers for a given type (if specified) or clears all handlers.
    /// </summary>
    /// <param name="eventType"></param>
    public void ClearPresenceEventHandlers(IRealtimePresence.EventType? eventType = null)
    {
        if (eventType != null && this.presenceEventListeners.TryGetValue(eventType.Value, out var list))
            list.Clear();
        else
            this.presenceEventListeners.Clear();
    }

    /// <summary>
    /// Notifies listeners of state changes
    /// </summary>
    /// <param name="eventType"></param>
    private void NotifyPresenceEventHandlers(IRealtimePresence.EventType eventType)
    {
        if (!this.presenceEventListeners.ContainsKey(eventType)) return;

        foreach (var handler in this.presenceEventListeners[eventType].ToArray())
            handler.Invoke(this, eventType);
    }

    /// <summary>
    /// Called in two cases:
    ///		- By `RealtimeChannel` when it receives a `presence_state` initializing message.
    ///		- By `RealtimeChannel` When a diff has been received and a new response is saved.
    /// </summary>
    /// <param name="response"></param>
    public void TriggerSync(SocketResponse response)
    {
        this.currentResponse = response;
        this.SetState();

        this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Sync);
    }

    /// <summary>
    /// Triggers a diff comparison and emits events accordingly.
    /// </summary>
    /// <param name="response"></param>
    /// <exception cref="ArgumentException"></exception>
    public void TriggerDiff(SocketResponse response)
    {
        if (response == null || response.Json == null)
            throw new ArgumentException(
                $"Expected parsable JSON response, instead received: `{JsonSerializer.Serialize(response, this.serializerSettings)}`");

        var obj = JsonSerializer.Deserialize<RealtimePresenceDiff<TPresenceModel>>(response.Json,
            this.serializerSettings);

        if (obj?.Payload == null) return;

        this.TriggerSync(response);

        if (obj.Payload.Joins!.Count > 0)
            this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Join);

        if (obj.Payload.Leaves!.Count > 0)
            this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Leave);
    }

    /// <summary>
    /// "Tracks" an event, used with <see cref="Presence"/>.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    [ImplementedCapability("realtime.presence.track")]
    public Task<Push> Track(object? payload, int timeoutMs = DefaultTimeout)
    {
        var eventName = Core.Helpers.GetMappedToAttr(ChannelEventName.Presence).Mapping;
        var push = new Push(this.channel.Socket, this.channel, eventName, "track",
            new Dictionary<string, object?> { { "event", "track" }, { "payload", payload } }, timeoutMs);

        var tcs = new TaskCompletionSource<Push>();

        void Handler(IRealtimePush<RealtimeChannel, SocketResponse> chanel, SocketResponse response) => tcs.TrySetResult(push);

        push.AddMessageReceivedHandler(Handler);

        push.OnTimeout += (sender, args) =>
        {
            if (sender is Push p)
                tcs.SetException(new RealtimeException($"Failed to send push [{p.Ref}])")
                { Reason = FailureHint.Reason.PushTimeout });
        };

        this.channel.Enqueue(push);

        return tcs.Task;
    }

    /// <summary>
    /// Untracks an event.
    /// </summary>
    [ImplementedCapability("realtime.presence.untrack")]
    public Task<Push> Untrack()
    {
        var eventName = Core.Helpers.GetMappedToAttr(ChannelEventName.Presence).Mapping;
        var push = new Push(this.channel.Socket, this.channel, eventName, "untrack",
            new Dictionary<string, object?> { { "event", "untrack" } });

        var tcs = new TaskCompletionSource<Push>();

        void Handler(IRealtimePush<RealtimeChannel, SocketResponse> chanel, SocketResponse response) => tcs.TrySetResult(push);

        push.AddMessageReceivedHandler(Handler);

        push.OnTimeout += (sender, args) =>
        {
            if (sender is Push p)
                tcs.TrySetException(new RealtimeException($"Failed to send push [{p.Ref}])")
                { Reason = FailureHint.Reason.PushTimeout });
        };

        this.channel.Enqueue(push);
        return tcs.Task;
    }

    /// <summary>
    /// Sets the internal Presence State from the <see cref="currentResponse"/>
    /// </summary>
    private void SetState()
    {
        this.LastState = new Dictionary<string, List<TPresenceModel>>(this.CurrentState);

        if (this.currentResponse?.Json == null) return;

        // Is a diff response?
        if (this.currentResponse.Payload!.Joins != null || this.currentResponse.Payload!.Leaves != null)
        {
            var state = JsonSerializer.Deserialize<RealtimePresenceDiff<TPresenceModel>>(this.currentResponse.Json,
                this.serializerSettings)!;

            if (state?.Payload == null) return;

            // Remove any result that has "left"
            foreach (var item in state.Payload.Leaves!)
                this.CurrentState.Remove(item.Key);

            // Add any results that have come in.
            foreach (var item in state.Payload.Joins!)
                this.CurrentState[item.Key] = item.Value.Metas!;
        }
        else
        {
            // It's a presence_state init response
            var state =
                JsonSerializer.Deserialize<PresenceStateSocketResponse<TPresenceModel>>(this.currentResponse.Json,
                    this.serializerSettings)!;

            if (state?.Payload == null) return;

            foreach (var item in state.Payload)
                this.CurrentState[item.Key] = item.Value.Metas!;
        }
    }
}
