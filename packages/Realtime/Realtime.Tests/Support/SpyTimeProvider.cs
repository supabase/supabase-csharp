using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Realtime.Tests.Support;

/// <summary>
///     Forwards to <paramref name="inner" /> while recording every timer the SDK creates: who owns its callback,
///     how often it fired, and whether it was disposed.
/// </summary>
internal sealed class SpyTimeProvider(TimeProvider inner) : TimeProvider
{
    private readonly List<SpyTimer> timers = [];

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
    public override long GetTimestamp() => inner.GetTimestamp();
    public override long TimestampFrequency => inner.TimestampFrequency;
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    /// <summary>Timers whose callback belongs to a <typeparamref name="TOwner" />, oldest first.</summary>
    public IReadOnlyList<SpyTimer> TimersOwnedBy<TOwner>()
    {
        lock (this.timers)
            return this.timers.Where(t => t.Owner is TOwner).ToList();
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var spy = new SpyTimer(callback.Target);
        spy.Inner = inner.CreateTimer(_ =>
        {
            Interlocked.Increment(ref spy.ticks);
            callback(state);
        }, null, dueTime, period);
        lock (this.timers)
            this.timers.Add(spy);
        return spy;
    }
}

internal sealed class SpyTimer(object? owner) : ITimer
{
    internal int ticks;
    internal ITimer Inner = null!;

    public object? Owner { get; } = owner;
    public int Ticks => Volatile.Read(ref this.ticks);
    public bool IsDisposed { get; private set; }

    public bool Change(TimeSpan dueTime, TimeSpan period) => this.Inner.Change(dueTime, period);

    public void Dispose()
    {
        this.IsDisposed = true;
        this.Inner.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        this.Dispose();
        return ValueTask.CompletedTask;
    }
}
