namespace Supabase.Core.Http;

/// <summary>
/// Whether the SDK adds the current trace context (<c>traceparent</c>, <c>tracestate</c>, <c>baggage</c>) to the
/// requests it sends.
/// </summary>
public enum TracePropagation
{
    /// <summary>Adds the context of <see cref="System.Diagnostics.Activity.Current"/> to every request, when there is one.</summary>
    Enabled,

    /// <summary>The SDK adds no trace context itself.</summary>
    Disabled
}
