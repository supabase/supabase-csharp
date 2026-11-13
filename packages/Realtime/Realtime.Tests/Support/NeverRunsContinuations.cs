using System.Threading;

namespace Realtime.Tests.Support;

/// <summary>
///     Plays a blocked UI thread: posted continuations are dropped, so anything that resumes on this context
///     never completes. Install it while starting a call, then restore the previous context before releasing
///     the call's awaits, so a captured context is posted to instead of running inline.
/// </summary>
internal sealed class NeverRunsContinuations : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) { }
}
