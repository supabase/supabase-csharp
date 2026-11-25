using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Realtime.Tests.Support;

internal static class FakeTimeProviderExtensions
{
    /// <summary>
    ///     Advances the clock by <paramref name="step" /> until <paramref name="done" /> completes, for work that
    ///     registers its timer on a background thread the test cannot await. Fails once <paramref name="patience" /> runs out.
    /// </summary>
    internal static async Task AdvanceUntilCompleted(this FakeTimeProvider clock, Task done, TimeSpan step, TimeSpan patience)
    {
        using var expiry = new CancellationTokenSource(patience);
        while (!done.IsCompleted)
        {
            expiry.Token.ThrowIfCancellationRequested();
            clock.Advance(step);
            await Task.Yield();
        }
    }
}
