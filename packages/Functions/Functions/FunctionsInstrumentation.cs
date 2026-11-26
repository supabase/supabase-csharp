using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using Supabase.Core.Diagnostics;

namespace Supabase.Functions;

/// <summary>
/// Diagnostics for the Functions client, exposed through <see cref="System.Diagnostics"/> so
/// consumers can subscribe with the OpenTelemetry SDK using <see cref="FunctionsDiagnostics.SourceName"/>
/// (<c>AddSource(...)</c> / <c>AddMeter(...)</c>). Emission is zero-cost when nothing is listening.
///
/// Telemetry must never carry secrets or PII: URLs are recorded without their query string and
/// no tag may contain the request body, a token, or other sensitive value. The invoked function
/// name is recorded (it is part of the path), but never the payload.
/// </summary>
internal static class FunctionsInstrumentation
{
    /// <summary>Tag key carrying the invoked function name (OpenTelemetry FaaS convention).</summary>
    internal const string FunctionNameTag = "faas.invoked_name";

    internal static readonly ActivitySource Source =
        Instrumentation.CreateActivitySource(typeof(FunctionsInstrumentation).Assembly, FunctionsDiagnostics.SourceName);

    private static readonly Meter Meter =
        Instrumentation.CreateMeter(typeof(FunctionsInstrumentation).Assembly, FunctionsDiagnostics.SourceName);

    private static readonly HttpClientRequestDuration RequestDuration = HttpClientRequestDuration.On(Meter);

    /// <summary>
    /// Starts the client span for a function invocation, tagged per OpenTelemetry HTTP
    /// conventions with the sanitized (query-less) URL. Returns null when nothing is listening.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI, sanitized before tagging.</param>
    /// <param name="functionName">The invoked function name.</param>
    internal static Activity? StartInvokeActivity(HttpMethod method, Uri uri, string functionName)
    {
        var activity = Source.StartActivity($"{method.Method} {uri.AbsolutePath}", ActivityKind.Client)
            .SetHttpRequestTags(method.Method, uri);

        activity?.SetTag(FunctionNameTag, functionName);
        return activity;
    }

    /// <summary>
    /// Records the invocation duration histogram for a request outcome.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI; only its scheme, host and port are recorded.</param>
    /// <param name="functionName">The invoked function name.</param>
    /// <param name="statusCode">The HTTP response status code, or null if the request never got one.</param>
    /// <param name="errorType">The error classification, or null on success.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> value captured before the request.</param>
    internal static void RecordInvoke(HttpMethod method, Uri uri, string functionName, int? statusCode, string? errorType, long startTimestamp)
    {
        if (RequestDuration.Enabled)
            RequestDuration.Record(startTimestamp, InvokeTags(method, uri, functionName, statusCode, errorType));
    }

    private static TagList InvokeTags(HttpMethod method, Uri uri, string functionName, int? statusCode, string? errorType)
    {
        var tags = HttpClientRequestDuration.TagsOf(method, uri, statusCode, errorType);
        tags.Add(FunctionNameTag, functionName);
        return tags;
    }
}
