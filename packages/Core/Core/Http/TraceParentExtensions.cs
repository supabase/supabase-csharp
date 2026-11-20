using System.Diagnostics;
using System.Net.Http.Headers;

namespace Supabase.Core.Http;

internal static class TraceParentExtensions
{
    private const string TraceParentHeader = "traceparent";
    private const string W3CVersion = "00";
    private const string NotSampledFlags = "00";

    /// <summary>
    /// Adds a <c>traceparent</c> that the SDK originated: a new span of <paramref name="traceId"/> that nothing records.
    /// A <c>traceparent</c> already in the headers is kept.
    /// </summary>
    internal static void AddUnsampledTraceParent(this HttpHeaders headers, ActivityTraceId traceId)
    {
        if (headers.Contains(TraceParentHeader))
            return;

        headers.TryAddWithoutValidation(TraceParentHeader, $"{W3CVersion}-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-{NotSampledFlags}");
    }
}
