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
[ImplementedCapability("realtime.subscriptions.subscribe_presence")]
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
    private readonly RealtimeChannel channel;
    private readonly JsonSerializerOptions serializerSettings;

    private readonly ConcurrentDictionary<IRealtimePresence.EventType, HandlerCollection<IRealtimePresence.PresenceEventHandler>>
        presenceEventListeners = new();

    private readonly HandlerCollection<Action<PresenceJoin<TPresenceModel>>> joinHandlers = new();
    private readonly HandlerCollection<Action<PresenceLeave<TPresenceModel>>> leaveHandlers = new();
    private Push? trackedJoin;

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
        IRealtimePresence.PresenceEventHandler handler) =>
        this.Add(this.presenceEventListeners.GetOrAdd(eventType, _ => new HandlerCollection<IRealtimePresence.PresenceEventHandler>()), handler);

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
        if (eventType is null or IRealtimePresence.EventType.Join)
            this.joinHandlers.Clear();

        if (eventType is null or IRealtimePresence.EventType.Leave)
            this.leaveHandlers.Clear();

        if (eventType != null && this.presenceEventListeners.TryGetValue(eventType.Value, out var list))
            list.Clear();
        else if (eventType == null)
            this.presenceEventListeners.Clear();
    }

    /// <summary>
    /// Adds a handler called once for each key that gains presences, with the presences that joined, before <see cref="CurrentState"/> changes.
    /// </summary>
    public void AddJoinHandler(Action<PresenceJoin<TPresenceModel>> handler) => this.Add(this.joinHandlers, handler);

    /// <summary>
    /// Adds a handler called once for each key that loses presences, with the presences that left, before <see cref="CurrentState"/> changes.
    /// </summary>
    public void AddLeaveHandler(Action<PresenceLeave<TPresenceModel>> handler) => this.Add(this.leaveHandlers, handler);

    private void Add<THandler>(HandlerCollection<THandler> handlers, THandler handler) where THandler : Delegate
    {
        var presenceOn = this.channel.PresenceOptions is { Enabled: true } || this.trackedJoin == this.channel.JoinPush;
        if ((this.channel.IsJoining || this.channel.IsJoined) && !presenceOn)
            throw new RealtimeException($"Cannot add presence handlers for {this.channel.Topic} after `Subscribe()` while presence is off.")
            {
                Reason = FailureHint.Reason.StateInvalid,
            };

        handlers.Add(handler);
    }

    internal PresenceOptions JoinOptions() =>
        this.joinHandlers.Snapshot().Length > 0 || this.leaveHandlers.Snapshot().Length > 0 || this.presenceEventListeners.Values.SelectMany(handlers => handlers.Snapshot()).Any()
            ? PresenceOptions.WithPresence(this.options.PresenceKey)
            : this.options;

    internal void End() => Debugger.Instance.Guard(this, () => this.Apply(new()));

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
    /// Called by `RealtimeChannel` when it receives a `presence_state` message, which replaces the state.
    /// </summary>
    /// <param name="response"></param>
    public void TriggerSync(SocketResponse response)
    {
        var state = JsonSerializer.Deserialize<PresenceStateSocketResponse<TPresenceModel>>(response.Json!,
            this.serializerSettings);

        if (state?.Payload == null) return;

        this.Apply(state.Payload.ToDictionary(item => item.Key, item => item.Value.Metas!));
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

        var next = new Dictionary<string, List<TPresenceModel>>(this.CurrentState);

        foreach (var item in obj.Payload.Joins!)
            next[item.Key] = Without(next.GetValueOrDefault(item.Key, []), item.Value.Metas!)
                .Concat(item.Value.Metas!)
                .ToList();

        foreach (var item in obj.Payload.Leaves!)
        {
            var metas = Without(next.GetValueOrDefault(item.Key, []), item.Value.Metas!);
            if (metas.Count > 0)
                next[item.Key] = metas;
            else
                next.Remove(item.Key);
        }

        this.Apply(next);

        var changes = PresenceChanges.From(obj.Payload, this.CurrentState);
        if (changes.HasJoins)
            this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Join);

        if (changes.HasLeaves)
            this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Leave);
    }

    private void Apply(Dictionary<string, List<TPresenceModel>> next)
    {
        this.Raise(this.joinHandlers, Changes(this.CurrentState, next)
            .Select(change => new PresenceJoin<TPresenceModel>(change.Key, change.Current, change.Changed)).ToList());
        this.Raise(this.leaveHandlers, Changes(next, this.CurrentState)
            .Select(change => new PresenceLeave<TPresenceModel>(change.Key, change.Current, change.Changed)).ToList());

        this.LastState = new Dictionary<string, List<TPresenceModel>>(this.CurrentState);
        this.CurrentState.Clear();
        foreach (var item in next)
            this.CurrentState[item.Key] = item.Value;

        this.NotifyPresenceEventHandlers(IRealtimePresence.EventType.Sync);
    }

    private void Raise<TChange>(HandlerCollection<Action<TChange>> handlers, IEnumerable<TChange> changes)
    {
        foreach (var handler in handlers.Snapshot())
            foreach (var change in changes)
                Debugger.Instance.Guard(this, () => handler.Invoke(change));
    }

    private static IEnumerable<(string Key, List<TPresenceModel> Current, List<TPresenceModel> Changed)> Changes(
        Dictionary<string, List<TPresenceModel>> before, Dictionary<string, List<TPresenceModel>> after) =>
        from item in after
        let current = before.GetValueOrDefault(item.Key, [])
        let changed = Without(item.Value, current)
        where changed.Count > 0
        select (item.Key, current, changed);

    private static List<TPresenceModel> Without(List<TPresenceModel> metas, List<TPresenceModel> removed) =>
        metas.Where(meta => removed.All(other => other.PhoenixRef != meta.PhoenixRef)).ToList();

    /// <summary>
    /// The joins and leaves of a presence diff that a handler should hear about.
    /// A key that replaces its metas (a new join and a leave of the previous <c>phx_ref</c>
    /// in the same diff) and is still present has neither joined nor left.
    /// </summary>
    private sealed class PresenceChanges
    {
        public bool HasJoins { get; }
        public bool HasLeaves { get; }

        private PresenceChanges(bool hasJoins, bool hasLeaves)
        {
            this.HasJoins = hasJoins;
            this.HasLeaves = hasLeaves;
        }

        public static PresenceChanges From(PresenceDiffPayload<TPresenceModel> diff,
            IReadOnlyDictionary<string, List<TPresenceModel>> currentState)
        {
            var replaced = diff.Joins!.Keys.Intersect(diff.Leaves!.Keys)
                .Where(currentState.ContainsKey).ToHashSet();

            return new PresenceChanges(
                diff.Joins.Keys.Except(replaced).Any(),
                diff.Leaves.Keys.Except(replaced).Any());
        }
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

        void Handler(IRealtimePush<RealtimeChannel, SocketResponse> chanel, SocketResponse response)
        {
            if (response.Payload?.Status == PhoenixStatusOk)
                this.trackedJoin = this.channel.JoinPush;

            tcs.TrySetResult(push);
        }

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
}
