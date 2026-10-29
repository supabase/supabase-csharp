#region

using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;

#endregion

namespace Gotrue.Tests.Support;

/// <summary>
///     Plays a caller that blocks on the task from a synchronization context that never runs posted continuations,
///     like a UI thread calling <c>.Wait()</c>: an SDK call that resumes on the caller's context never completes.
/// </summary>
internal static class BlockingCaller
{
    private static readonly TimeSpan DeadlockTimeout = TimeSpan.FromSeconds(5);

    internal static bool Completes(Func<Task> call)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(Substitute.For<SynchronizationContext>());
        try
        {
            return call().Wait(DeadlockTimeout);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
