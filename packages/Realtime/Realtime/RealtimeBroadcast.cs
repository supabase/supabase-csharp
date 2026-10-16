using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime;

/// <summary>
/// Represents a realtime broadcast client.
/// 
/// Broadcast follows the publish-subscribe pattern where a client publishes messages to a channel with a unique identifier.
/// Other clients can elect to receive the message in real-time by subscribing to the channel with the same unique identifier. If these clients are online and subscribed then they will receive the message.
///
/// Broadcast works by connecting your client to the nearest Realtime server, which will communicate with other servers to relay messages to other clients.
/// A common use-case is sharing a user's cursor position with other clients in an online game.
/// </summary>
/// <typeparam name="TBroadcastModel">A model representing expected payload.</typeparam>
[ImplementedCapability("realtime.subscriptions.broadcast", "Typed broadcast payloads via generics; better-typed than JS on('broadcast', ...).")]
public class RealtimeBroadcast<TBroadcastModel> : IRealtimeBroadcast where TBroadcastModel : BaseBroadcast
{
    private readonly RealtimeChannel channel;
    private readonly JsonSerializerOptions serializerSettings;

    private SocketResponse? lastSocketResponse;

    private readonly List<IRealtimeBroadcast.BroadcastEventHandler> broadcastEventHandlers = new();

    /// <summary>
    /// The last received broadcast.
    /// </summary>
    public TBroadcastModel? Current()
    {
        if (this.lastSocketResponse == null) return null;

        var obj = JsonSerializer.Deserialize<SocketResponse<TBroadcastModel>>(this.lastSocketResponse.Json!,
            this.serializerSettings);

        if (obj == null || obj.Payload == null) return null;

        return obj.Payload;
    }

    /// <summary>
    /// Initializes a realtime broadcast helper class.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="options"></param>
    /// <param name="serializerSettings"></param>
    public RealtimeBroadcast(RealtimeChannel channel, BroadcastOptions options,
        JsonSerializerOptions serializerSettings)
    {
        this.channel = channel;
        this.serializerSettings = serializerSettings;
    }

    /// <summary>
    /// Adds a broadcast event listener.
    /// </summary>
    /// <param name="broadcastEventHandler"></param>
    public void AddBroadcastEventHandler(IRealtimeBroadcast.BroadcastEventHandler broadcastEventHandler)
    {
        if (!this.broadcastEventHandlers.Contains(broadcastEventHandler))
            this.broadcastEventHandlers.Add(broadcastEventHandler);
    }

    /// <summary>
    /// Removes a broadcast event listener.
    /// </summary>
    /// <param name="broadcastEventHandler"></param>
    public void RemoveBroadcastEventHandler(IRealtimeBroadcast.BroadcastEventHandler broadcastEventHandler)
    {
        if (this.broadcastEventHandlers.Contains(broadcastEventHandler))
            this.broadcastEventHandlers.Remove(broadcastEventHandler);
    }

    /// <summary>
    /// Clears all broadcast event listeners
    /// </summary>
    public void ClearBroadcastEventHandlers() =>
        this.broadcastEventHandlers.Clear();

    private void NotifyBroadcastEventHandlers()
    {
        foreach (var handler in this.broadcastEventHandlers.ToArray())
            handler.Invoke(this, this.Current());
    }

    /// <summary>
    /// Called by <see cref="RealtimeChannel"/> when a broadcast event is received, then parsed/typed here.
    /// </summary>
    /// <param name="response"></param>
    /// <exception cref="ArgumentException"></exception>
    public void TriggerReceived(SocketResponse response)
    {
        if (response == null || response.Json == null)
            throw new ArgumentException(
                $"Expected parsable JSON response, instead received: `{JsonSerializer.Serialize(response, this.serializerSettings)}`");

        this.lastSocketResponse = response;
        this.NotifyBroadcastEventHandlers();
    }

    /// <summary>
    /// Broadcasts an arbitrary payload
    /// </summary>
    /// <param name="broadcastEventName"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    public Task<bool> Send(string? broadcastEventName, object payload, int timeoutMs = 10000)
    {
        if (payload is BaseBroadcast baseBroadcast && string.IsNullOrEmpty(baseBroadcast.Event))
            baseBroadcast.Event = broadcastEventName;

        return this.channel.Send(ChannelEventName.Broadcast, broadcastEventName, payload, timeoutMs);
    }
}
