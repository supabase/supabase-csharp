using System;
using System.Globalization;

namespace Supabase.Realtime.Broadcast;

/// <summary>
/// Asks the server to store a broadcast sent on a private channel, so it can be replayed when a channel joins.
/// </summary>
public sealed class BroadcastPersistence
{
    /// <summary>
    /// Stores the broadcast for <paramref name="ttl"/>, sent in whole seconds, or for the maximum retention when null.
    /// </summary>
    public BroadcastPersistence(TimeSpan? ttl = null) => this.Ttl = ttl;

    /// <summary>
    /// How long the server keeps the broadcast. Null keeps it for the maximum retention.
    /// </summary>
    public TimeSpan? Ttl { get; }

    internal string Metadata => this.Ttl == null ? "{\"persist\":true}" : $"{{\"persist\":{{\"ttl\":{this.TtlSeconds}}}}}";

    internal string Query => this.Ttl == null ? "persist=true" : $"persist[ttl]={this.TtlSeconds}";

    private string TtlSeconds => ((long) this.Ttl!.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
}
