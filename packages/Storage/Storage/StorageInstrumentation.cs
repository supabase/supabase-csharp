using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using Supabase.Core.Diagnostics;

namespace Supabase.Storage;

/// <summary>
/// Diagnostics for the Storage client, exposed through <see cref="System.Diagnostics"/> so
/// consumers can subscribe with the OpenTelemetry SDK using <see cref="StorageDiagnostics.SourceName"/>
/// (<c>AddSource(...)</c> / <c>AddMeter(...)</c>). Emission is zero-cost when nothing is listening.
///
/// Telemetry must never carry secrets or PII: URLs are recorded without their query string
/// (Storage signed URLs put a <c>token</c> there) and no tag may contain a token, credential,
/// or file contents.
///
/// Storage covers three HTTP surfaces: the JSON control plane (bucket/file management), and the
/// upload and download transfer paths. Transfers additionally report the number of bytes moved,
/// since a duration alone does not describe a file transfer.
/// </summary>
internal static class StorageInstrumentation
{
    /// <summary>Tag key carrying the transfer direction (upload/download).</summary>
    internal const string DirectionTag = "storage.transfer.direction";

    internal const string DirectionUpload = "upload";
    internal const string DirectionDownload = "download";

    internal static readonly ActivitySource Source =
        Instrumentation.CreateActivitySource(typeof(StorageInstrumentation).Assembly, StorageDiagnostics.SourceName);

    private static readonly Meter Meter =
        Instrumentation.CreateMeter(typeof(StorageInstrumentation).Assembly, StorageDiagnostics.SourceName);

    private static readonly HttpClientRequestDuration RequestDuration = HttpClientRequestDuration.On(Meter);

    private static readonly Histogram<double> TransferDuration = Meter.CreateHistogram<double>(
        "supabase.storage.transfer.duration", "s", "Duration of file upload/download transfers.");

    private static readonly Histogram<long> TransferSize = Meter.CreateHistogram<long>(
        "supabase.storage.transfer.size", "By", "Number of bytes moved by file upload/download transfers.");

    /// <summary>
    /// Starts a client span for a Storage HTTP request, tagged per OpenTelemetry HTTP
    /// conventions with the sanitized (query-less) URL. Returns null when nothing is listening.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI, sanitized before tagging.</param>
    /// <param name="direction">The transfer direction, or null for control-plane requests.</param>
    internal static Activity? StartHttpActivity(HttpMethod method, Uri uri, string? direction = null)
    {
        var activity = Source.StartActivity($"{method.Method} {uri.AbsolutePath}", ActivityKind.Client)
            .SetHttpRequestTags(method.Method, uri);

        if (direction != null)
            activity?.SetTag(DirectionTag, direction);

        return activity;
    }

    /// <summary>
    /// Records the control-plane request duration histogram for an HTTP request outcome.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI; only its scheme, host and port are recorded.</param>
    /// <param name="statusCode">The HTTP response status code, or null if the request never got one.</param>
    /// <param name="errorType">The error classification, or null on success.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> value captured before the request.</param>
    internal static void RecordRequest(HttpMethod method, Uri uri, int? statusCode, string? errorType, long startTimestamp)
    {
        if (RequestDuration.Enabled)
            RequestDuration.Record(startTimestamp, HttpClientRequestDuration.TagsOf(method, uri, statusCode, errorType));
    }

    /// <summary>
    /// Records the transfer duration and (when known) size histograms for an upload/download outcome.
    /// </summary>
    /// <param name="direction">The transfer direction (<see cref="DirectionUpload"/> / <see cref="DirectionDownload"/>).</param>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI; only its host and path are recorded.</param>
    /// <param name="bytes">The number of bytes transferred, or null if it could not be determined.</param>
    /// <param name="statusCode">The HTTP response status code, or null if the request never got one.</param>
    /// <param name="errorType">The error classification, or null on success.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> value captured before the request.</param>
    internal static void RecordTransfer(string direction, HttpMethod method, Uri uri, long? bytes, int? statusCode, string? errorType, long startTimestamp)
    {
        if (!TransferDuration.Enabled && !TransferSize.Enabled)
            return;

        var tags = TransferTags(direction, method.Method, uri, statusCode, errorType);
        TransferDuration.Record(Instrumentation.SecondsSince(startTimestamp), tags);

        if (bytes.HasValue)
            TransferSize.Record(bytes.Value, tags);
    }

    private static TagList TransferTags(string direction, string method, Uri uri, int? statusCode, string? errorType)
    {
        var tags = new TagList
        {
            { "http.request.method", method },
            { "server.address", uri.Host },
            { "url.path", uri.AbsolutePath },
            { DirectionTag, direction }
        };

        if (statusCode.HasValue)
            tags.Add("http.response.status_code", statusCode.Value);

        if (errorType != null)
            tags.Add("error.type", errorType);

        return tags;
    }
}
