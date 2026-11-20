using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Supabase.Core.Http;

internal static class TraceContext
{
    /// <summary>
    /// Adds the trace context to <paramref name="headers"/>: the current activity's when there is one, otherwise a new
    /// unsampled <c>traceparent</c> so every request carries a trace id. A header the caller already set is kept.
    /// </summary>
    /// <param name="headers">The headers of the request being sent.</param>
    /// <param name="propagation">Whether the SDK may add trace context.</param>
    /// <param name="originatedTraceId">The trace id of the call when it spans several requests, so they share a trace.</param>
    internal static void Inject(HttpHeaders headers, TracePropagation propagation, ActivityTraceId? originatedTraceId = null)
    {
        if (propagation == TracePropagation.Disabled)
            return;

        var activity = Activity.Current;
        if (activity is null)
        {
            headers.AddUnsampledTraceParent(originatedTraceId ?? ActivityTraceId.CreateRandom());
            return;
        }

        try
        {
            DistributedContextPropagator.Current.Inject(activity, headers, AddUnlessPresent);
        }
        catch (Exception)
        {
        }
    }

    private static void AddUnlessPresent(object? carrier, string name, string value)
    {
        var headers = (HttpHeaders) carrier!;
        if (!headers.Contains(name))
            headers.TryAddWithoutValidation(name, value);
    }
}
