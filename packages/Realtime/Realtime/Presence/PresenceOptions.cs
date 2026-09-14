using System.Text.Json.Serialization;

namespace Supabase.Realtime.Presence;

/// <summary>
/// Options used to initialize Realtime Presence
/// </summary>
public class PresenceOptions
{
    /// <summary>
    /// Key option is used to track presence payload across clients
    /// </summary>
    [JsonPropertyName("key")]
    public string PresenceKey { get; set; }

    /// <summary>
    /// Whether presence tracking is enabled for this channel join. Requires
    /// <c>enabled: true</c> to receive the initial <c>presence_state</c> sync.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; private set; }

    /// <summary>
    /// Presence options with tracking enabled. Equivalent to <see cref="WithPresence"/>.
    /// </summary>
    /// <param name="presenceKey">used to track presence payload across clients</param>
    public PresenceOptions(string presenceKey)
    {
        this.PresenceKey = presenceKey;
        this.Enabled = true;
    }

    /// <summary>
    /// Creates presence options that receive the initial <c>presence_state</c> sync and updates from other clients.
    /// </summary>
    /// <param name="presenceKey">Used to track presence payload across clients</param>
    public static PresenceOptions WithPresence(string presenceKey) => new(presenceKey);

    /// <summary>
    /// Creates presence options that do not receive presence updates from other clients. This client can still make itself visible to others via <c>Track</c>.
    /// </summary>
    /// <param name="presenceKey">Used to track presence payload across clients</param>
    public static PresenceOptions WithoutPresence(string presenceKey) => new(presenceKey) { Enabled = false };
}
