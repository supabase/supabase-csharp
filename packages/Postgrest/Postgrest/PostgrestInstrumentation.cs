using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using Supabase.Core.Diagnostics;

namespace Supabase.Postgrest;

/// <summary>
/// Diagnostics for the Postgrest client, exposed through <see cref="System.Diagnostics"/> so
/// consumers can subscribe with the OpenTelemetry SDK using <see cref="PostgrestDiagnostics.SourceName"/>
/// (<c>AddSource(...)</c> / <c>AddMeter(...)</c>). Emission is zero-cost when nothing is listening.
///
/// Telemetry must never carry secrets or PII: the span URL goes through <see cref="RedactedUrl"/>
/// (Postgrest puts column filters and their values in the query string), the metric carries no URL,
/// and no tag may contain row data, a credential, or other sensitive value.
/// </summary>
internal static class PostgrestInstrumentation
{
    /// <summary>Tag key carrying the logical operation (select/insert/update/upsert/delete/rpc).</summary>
    internal const string OperationTag = "supabase.postgrest.operation";

    /// <summary>Tag key carrying the table a table call targets.</summary>
    internal const string TableTag = "supabase.postgrest.table";

    /// <summary>Tag key carrying the function an RPC call invokes.</summary>
    internal const string FunctionTag = "supabase.postgrest.function";

    internal static readonly ActivitySource Source =
        Instrumentation.CreateActivitySource(typeof(PostgrestInstrumentation).Assembly, PostgrestDiagnostics.SourceName);

    private static readonly Meter Meter =
        Instrumentation.CreateMeter(typeof(PostgrestInstrumentation).Assembly, PostgrestDiagnostics.SourceName);

    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "http.client.request.duration", "s", "Duration of HTTP client requests.");

    /// <summary>
    /// Maps an HTTP method and the request flags to the logical Postgrest operation name.
    /// A HEAD request (a count) is a read, so it is a select.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="isInsert">Whether the request is an insert.</param>
    /// <param name="isUpdate">Whether the request is an update.</param>
    /// <param name="isUpsert">Whether the request is an upsert.</param>
    internal static string ResolveOperation(HttpMethod method, bool isInsert, bool isUpdate, bool isUpsert)
    {
        if (isUpsert) return "upsert";
        if (isInsert) return "insert";
        if (isUpdate) return "update";
        if (method == HttpMethod.Delete) return "delete";
        return "select";
    }

    /// <summary>
    /// Starts the client span for an outgoing HTTP request, tagged per OpenTelemetry HTTP
    /// conventions with the URL's query values redacted. Returns null when nothing is listening.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI; its query values are redacted before tagging.</param>
    /// <param name="call">What the request is about.</param>
    internal static Activity? StartHttpActivity(HttpMethod method, Uri uri, PostgrestCall call)
    {
        var activity = Source.StartActivity($"{method.Method} {uri.AbsolutePath}", ActivityKind.Client)
            .SetHttpRequestTags(method.Method, uri);

        activity?.SetTag("url.full", RedactedUrl.Of(uri));
        activity?.SetTag(OperationTag, call.Operation);

        if (call.Table != null)
            activity?.SetTag(TableTag, call.Table);

        if (call.Function != null)
            activity?.SetTag(FunctionTag, call.Function);

        return activity;
    }

    /// <summary>
    /// Records the request duration histogram for an HTTP request outcome.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI; only its scheme, host and port are recorded.</param>
    /// <param name="call">What the request is about.</param>
    /// <param name="statusCode">The HTTP response status code, or null if the request never got one.</param>
    /// <param name="errorType">The error classification, or null on success.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> value captured before the request.</param>
    internal static void RecordRequest(HttpMethod method, Uri uri, PostgrestCall call, int? statusCode, string? errorType, long startTimestamp)
    {
        var tags = new TagList
        {
            { "http.request.method", method.Method },
            { "server.address", uri.Host },
            { "server.port", uri.Port },
            { "url.scheme", uri.Scheme },
            { OperationTag, call.Operation }
        };

        if (call.Table != null)
            tags.Add(TableTag, call.Table);

        if (call.Function != null)
            tags.Add(FunctionTag, call.Function);

        if (statusCode.HasValue)
            tags.Add("http.response.status_code", statusCode.Value);

        if (errorType != null)
            tags.Add("error.type", errorType);

        RequestDuration.Record(GetElapsedSeconds(startTimestamp), tags);
    }

    private static double GetElapsedSeconds(long startTimestamp) =>
        (Stopwatch.GetTimestamp() - startTimestamp) / (double) Stopwatch.Frequency;
}
