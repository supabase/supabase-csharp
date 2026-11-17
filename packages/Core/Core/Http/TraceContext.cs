using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Supabase.Core.Http;

internal static class TraceContext
{
    internal static void Inject(HttpHeaders headers, TracePropagation propagation)
    {
        var activity = Activity.Current;
        if (propagation == TracePropagation.Disabled || activity is null)
            return;

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
