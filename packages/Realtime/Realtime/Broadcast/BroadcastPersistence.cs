using System;
using System.Globalization;

namespace Supabase.Realtime.Broadcast;

/// <summary>
/// Asks the server to store a broadcast sent on a private channel, so it can be replayed when a channel joins.
/// </summary>
/// <example>
/// <code>
/// await channel.HttpSendPersistedAsync("chat", new { text = "hi" }, BroadcastPersistence.For(TimeSpan.FromHours(1)));
/// await channel.HttpSendPersistedAsync("chat", new { text = "hi" }, BroadcastPersistence.ForMaximumRetention);
/// </code>
/// </example>
public sealed record BroadcastPersistence
{
    private readonly long? ttlSeconds;

    private BroadcastPersistence(long? ttlSeconds) => this.ttlSeconds = ttlSeconds;

    /// <summary>
    /// Stores the broadcast for the longest retention the server allows.
    /// </summary>
    public static BroadcastPersistence ForMaximumRetention { get; } = new((long?) null);

    /// <summary>
    /// Stores the broadcast for <paramref name="ttl"/>. The server owns the longest ttl it accepts, and rejects a
    /// longer one.
    /// </summary>
    /// <param name="ttl">How long the server keeps the broadcast, a positive whole number of seconds.</param>
    /// <exception cref="ArgumentException"><paramref name="ttl"/> is not a positive whole number of seconds, which is all the server reads.</exception>
    public static BroadcastPersistence For(TimeSpan ttl) =>
        IsPositiveWholeSeconds(ttl)
            ? new BroadcastPersistence(ttl.Ticks / TimeSpan.TicksPerSecond)
            : throw new ArgumentException("The ttl of a persisted broadcast must be a positive whole number of seconds.", nameof(ttl));

    internal string Metadata => this.ttlSeconds is { } seconds ? $"{{\"persist\":{{\"ttl\":{Invariant(seconds)}}}}}" : "{\"persist\":true}";

    internal string Query => this.ttlSeconds is { } seconds ? $"persist[ttl]={Invariant(seconds)}" : "persist=true";

    private static bool IsPositiveWholeSeconds(TimeSpan ttl) => ttl > TimeSpan.Zero && ttl.Ticks % TimeSpan.TicksPerSecond == 0;

    private static string Invariant(long seconds) => seconds.ToString(CultureInfo.InvariantCulture);
}
