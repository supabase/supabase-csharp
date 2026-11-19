using System.Text.Json;
using System.Text.Json.Serialization;
using Supabase.Realtime.Interfaces;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Socket;

/// <summary>
/// A SocketResponse with support for Generically typed Payload
/// </summary>
/// <typeparam name="T"></typeparam>
public class SocketResponse<T> : SocketResponse where T : class
{
    /// <summary>
    /// Parameterless constructor used by System.Text.Json when deserializing.
    /// </summary>
    public SocketResponse() { }

    /// <inheritdoc />
    public SocketResponse(JsonSerializerOptions serializerSettings) : base(serializerSettings)
    { }

    /// <summary>
    /// The typed payload response
    /// </summary>
    [JsonPropertyName("payload")]
    public new T? Payload { get; set; }
}

/// <summary>
/// Representation of a Socket Response.
/// </summary>
public class SocketResponse : IRealtimeSocketResponse
{
    internal JsonSerializerOptions? SerializerSettings;

    /// <summary>
    /// Parameterless constructor used by System.Text.Json when deserializing. Callers set
    /// <see cref="SerializerSettings"/> afterward so the typed payload can be re-parsed.
    /// </summary>
    public SocketResponse() { }

    /// <summary>
    /// Represents a socket response
    /// </summary>
    /// <param name="serializerSettings"></param>
    public SocketResponse(JsonSerializerOptions serializerSettings) => this.SerializerSettings = serializerSettings;

    /// <summary>
    /// The internal realtime topic.
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    /// <summary>
    /// The internal, raw event given by the socket
    /// </summary>
    [JsonPropertyName("event")]
    public string? _event { get; set; }

    /// <summary>
    /// The typed, parsed event given by this library.
    /// </summary>
    [JsonIgnore]
    public EventType Event
    {
        get
        {
            return this._event switch
            {
                ChannelEventPresenceState => EventType.PresenceState,
                ChannelEventPresenceDiff => EventType.PresenceDiff,
                ChannelEventBroadcast => EventType.Broadcast,
                ChannelEventPostgresChanges => EventType.PostgresChanges,
                ChannelEventSystem => EventType.System,
                ChannelEventReply => EventType.PostgresChanges,
                _ => this.Payload?.Type ?? EventType.Unknown
            };
        }
    }

    /// <summary>
    /// The payload/response.
    /// </summary>
    [JsonPropertyName("payload")]
    public SocketResponsePayload? Payload { get; set; }

    /// <summary>
    /// An internal reference to this particular feedback loop.
    /// </summary>
    [JsonPropertyName("ref")]
    public string? Ref { get; set; }

    /// <summary>
    /// The raw JSON string of the received data.
    /// </summary>
    [JsonIgnore]
    internal string? Json { get; set; }

    /// <summary>
    /// The bytes of a received broadcast whose payload is not JSON.
    /// </summary>
    [JsonIgnore]
    internal byte[]? BinaryPayload { get; set; }
}
