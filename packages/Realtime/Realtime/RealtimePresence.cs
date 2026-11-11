using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
[PartialCapability("realtime.subscriptions.subscribe_presence", "Join and Leave handlers get no key or presences, and a presence_state snapshot raises Sync only, not Join or Leave.")]
public class RealtimePresence<TPresenceModel> : IRealtimePresence where TPresenceModel : BasePresence
{
    /// <summary>
    /// The Last State of this Presence instance.
    /// </summary>
    [ImplementedCapability("realtime.presence.presence_state")]
    public Dictionary<string, List<TPresenceModel>> LastState { get; private set; } =
        new();

    /// <summary>
    /// The Current State of this Presence instance.
    /// </summary>
    [ImplementedCapability("realtime.presence.presence_state")]
    public Dictionary<string, List<TPresenceModel>> CurrentState { get; } = new();

    private PresenceOptions options;
    private SocketResponse? currentResponse;
    private readonly RealtimeChannel channel;
    private readonly JsonSerializerOptions serializerSettings;

    private readonly ConcurrentDictionary<IRealtimePresence.EventType, HandlerCollection<IRealtimePresence.PresenceEventHandler>>
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
        this.presenceEventListeners.GetOrAdd(eventType, _ => new HandlerCollection<IRealtimePresence.PresenceEventHandler>())
            .Add(handler);
    }

    /// <summary>
    /// Remove an event handler
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="handler"></param>
    public void RemovePresenceEventHandlers(IRealtimePresence.EventType eventType,
        IRealtimePresence.PresenceEventHandler handler)
    {
        if (this.presenceEventListeners.TryGetValue(eventType, out var list))
            list.Remove(handler);
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
        if (!this.presenceEventListeners.TryGetValue(eventType, out var list)) return;

        foreach (var handler in list.Snapshot())
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

        // Tracking someone who is already present puts the new meta in joins and the
        // previous phx_ref in leaves. SetState keeps that key. Those two entries cancel:
        // the caller would otherwise see Leave after Join and treat the user as gone.
        if (obj.Payload.Joins!.Keys.Any(key => !this.IsTrackedUpdate(key, obj.Payload)))
            this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Join);

        if (obj.Payload.Leaves!.Keys.Any(key => !this.IsTrackedUpdate(key, obj.Payload)))
            this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Leave);
    }

    /// <summary>
    /// True when <paramref name="key"/> is in both sides of this diff and still present.
    /// That is a payload update (new <c>phx_ref</c>, leave of <c>phx_ref_prev</c>), not a join or a leave.
    /// </summary>
    private bool IsTrackedUpdate(string key, PresenceDiffPayload<TPresenceModel> payload) =>
        payload.Joins!.ContainsKey(key)
        && payload.Leaves!.ContainsKey(key)
        && this.CurrentState.ContainsKey(key);

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

            foreach (var item in state.Payload.Joins!)
            {
                var joined = item.Value.Metas!;
                var joinedRefs = joined.Select(m => m.PhoenixRef).ToHashSet();
                this.CurrentState[item.Key] = this.CurrentState.GetValueOrDefault(item.Key, [])
                    .Where(m => !joinedRefs.Contains(m.PhoenixRef))
                    .Concat(joined)
                    .ToList();
            }

            foreach (var item in state.Payload.Leaves!)
            {
                var leftRefs = item.Value.Metas!.Select(m => m.PhoenixRef).ToHashSet();
                var metas = this.CurrentState.GetValueOrDefault(item.Key, [])
                    .Where(m => !leftRefs.Contains(m.PhoenixRef))
                    .ToList();
                if (metas.Count > 0)
                    this.CurrentState[item.Key] = metas;
                else
                    this.CurrentState.Remove(item.Key);
            }
        }
        else
        {
            // It's a presence_state init response
            var state =
                JsonSerializer.Deserialize<PresenceStateSocketResponse<TPresenceModel>>(this.currentResponse.Json,
                    this.serializerSettings)!;

            if (state?.Payload == null) return;

            this.CurrentState.Clear();
            foreach (var item in state.Payload)
                this.CurrentState[item.Key] = item.Value.Metas!;
        }
    }
}
