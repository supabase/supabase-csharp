using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;

namespace Supabase.Core.Diagnostics;

/// <summary>
/// The OpenTelemetry <c>http.client.request.duration</c> histogram on a client library's <see cref="Meter"/>.
/// The meter names the product, so every Supabase client records the same instrument with the same HTTP
/// attributes and adds its own on top. The attributes carry no URL, query string, or credential.
/// </summary>
internal sealed class HttpClientRequestDuration
{
    private const string Name = "http.client.request.duration";
    private const string Unit = "s";
    private const string Description = "Duration of HTTP client requests.";

    private readonly Histogram<double> histogram;

    private HttpClientRequestDuration(Histogram<double> histogram) => this.histogram = histogram;

    /// <summary>
    /// Whether anything listens to the histogram. Callers build attributes only when it does, so recording
    /// costs nothing without a listener.
    /// </summary>
    internal bool Enabled => this.histogram.Enabled;

    /// <summary>
    /// Creates the histogram on the meter of the client library that sends the requests.
    /// </summary>
    /// <param name="meter">The meter that names the client library.</param>
    internal static HttpClientRequestDuration On(Meter meter) =>
        new(meter.CreateHistogram<double>(Name, Unit, Description));

    /// <summary>
    /// The OpenTelemetry HTTP client attributes of a request outcome: method, server address and port, scheme,
    /// and the status code and error type when there is one.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="uri">The request URI; only its scheme, host and port are recorded.</param>
    /// <param name="statusCode">The HTTP response status code, or null if the request never got one.</param>
    /// <param name="errorType">The error classification, or null on success.</param>
    internal static TagList TagsOf(HttpMethod method, Uri uri, int? statusCode, string? errorType)
    {
        var tags = new TagList
        {
            { "http.request.method", method.Method },
            { "server.address", uri.Host },
            { "server.port", uri.Port },
            { "url.scheme", uri.Scheme }
        };

        if (statusCode.HasValue)
            tags.Add("http.response.status_code", statusCode.Value);

        if (errorType != null)
            tags.Add("error.type", errorType);

        return tags;
    }

    /// <summary>
    /// Records the time elapsed since the request started.
    /// </summary>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> value captured before the request.</param>
    /// <param name="tags">The attributes of the request outcome.</param>
    internal void Record(long startTimestamp, in TagList tags) =>
        this.histogram.Record(Instrumentation.SecondsSince(startTimestamp), tags);
}
