using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Realtime.Models;

/// <summary>
/// Represents a Broadcast response with a modeled payload.
/// </summary>
/// <typeparam name="T"></typeparam>
public class BaseBroadcast<T> : BaseBroadcast where T : class
{
    /// <summary>
    /// The typed payload.
    /// </summary>
    [JsonPropertyName("payload")]
    public new T? Payload { get; set; }
}

/// <summary>
/// Represents an arbitrary Broadcast response.
/// </summary>
public class BaseBroadcast
{
    /// <summary>
    /// The event.
    /// </summary>
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    /// <summary>
    /// The payload.
    /// </summary>
    [JsonPropertyName("payload")]
    public Dictionary<string, object>? Payload { get; set; }

    /// <summary>
    /// Additional metadata associated with a broadcast event. Populated by the server when a
    /// message is replayed from history on a private channel; otherwise absent.
    /// </summary>
    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BroadcastMeta? Meta { get; set; }

    /// <summary>
    /// The payload of a broadcast that was sent as bytes, received on a client that speaks
    /// <see cref="ProtocolVersion.V2"/>. Null for a broadcast with a JSON payload.
    /// </summary>
    [JsonIgnore]
    public byte[]? BinaryPayload { get; internal set; }
}

/// <summary>
/// Server-supplied metadata attached to a broadcast event, present when the message was replayed
/// from history on a private channel.
/// </summary>
public class BroadcastMeta
{
    /// <summary>
    /// The unique identifier the server assigned to the broadcast message.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Whether this event was replayed from history rather than received live.
    /// </summary>
    [JsonPropertyName("replayed")]
    public bool Replayed { get; set; }
}
