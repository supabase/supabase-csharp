namespace Supabase.Core.Http;

/// <summary>
/// Whether the SDK adds trace context (<c>traceparent</c>, <c>tracestate</c>, <c>baggage</c>) to the requests it sends.
/// </summary>
public enum TracePropagation
{
    /// <summary>
    /// Adds the context of <see cref="System.Diagnostics.Activity.Current"/> to every request. Without an active activity, adds
    /// an unsampled <c>traceparent</c> (flags <c>00</c>) with a new trace id per call, shared by its retries. A <c>traceparent</c> already
    /// set on the request is kept.
    /// </summary>
    Enabled,

    /// <summary>The SDK adds no trace context itself.</summary>
    Disabled
}
