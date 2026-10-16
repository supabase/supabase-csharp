using System.Text.Json.Serialization;
using Supabase.Core.Attributes;

namespace Supabase.Realtime.Broadcast;

/// <summary>
/// Configures broadcast behavior for a channel: whether the client receives its own messages,
/// whether the server acknowledges sends, and whether past messages are replayed from history.
/// </summary>
public class BroadcastOptions
{
    /// <summary>
    /// self option enables client to receive message it broadcast
    /// </summary>
    [JsonPropertyName("self")]
    [ImplementedCapability("realtime.subscriptions.broadcast_self")]
    public bool BroadcastSelf { get; set; } = false;

    /// <summary>
    /// ack option instructs server to acknowledge that broadcast message was received
    /// </summary>
    [JsonPropertyName("ack")]
    [ImplementedCapability("realtime.subscriptions.broadcast_ack")]
    public bool BroadcastAck { get; set; } = false;

    /// <summary>
    /// replay option instructs server to replay broadcast messages
    /// </summary>
    [JsonPropertyName("replay")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReplayOptions? Replay { get; set; }

    /// <summary>
    /// Options for replaying events in broadcast configurations.
    /// </summary>
    public class ReplayOptions
    {
        /// <summary>
        /// Specifies the starting point in time, in milliseconds since the Unix epoch,
        /// from which events should be replayed in the broadcast configuration.
        /// </summary>
        [JsonPropertyName("since")]
        public long Since { get; set; }

        /// <summary>
        /// Specifies the maximum number of events to be replayed during broadcast.
        /// When set to null, there is no limit to the number of events replayed.
        /// </summary>
        [JsonPropertyName("limit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Limit { get; set; }
    }

    /// <summary>
    /// Initializes broadcast options
    /// </summary>
    /// <param name="broadcastSelf"></param>
    /// <param name="broadcastAck"></param>
    public BroadcastOptions(bool broadcastSelf = false, bool broadcastAck = false)
    {
        this.BroadcastSelf = broadcastSelf;
        this.BroadcastAck = broadcastAck;
    }
}
