using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Supabase.Core.Logging;

namespace Supabase.Core.Http;

/// <summary>Sends a request, retrying with backoff per <see cref="RetryOptions"/>. Default options send once, un-retried.</summary>
public static class RetryExecutor
{
    private static readonly ThreadLocal<Random> RandomProvider = new(() => new Random(Guid.NewGuid().GetHashCode()));

    /// <summary>Sends via <paramref name="requestFactory"/>, called fresh per attempt since a sent <see cref="HttpRequestMessage"/> can't be reused.</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory,
        RetryOptions options, CancellationToken cancellationToken = default) =>
        SendAsync(client, requestFactory, options, HttpCompletionOption.ResponseContentRead, cancellationToken);

    internal static Task<HttpResponseMessage> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory,
        RetryOptions options, TracePropagation tracePropagation, ILogger? logger, CancellationToken cancellationToken) =>
        SendAsync(client, requestFactory, options, tracePropagation, logger, HttpCompletionOption.ResponseContentRead, cancellationToken);

    /// <summary>Sends a request with the specified <paramref name="completionOption"/>.</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory,
        RetryOptions options, HttpCompletionOption completionOption, CancellationToken cancellationToken) =>
        SendAsync(client, requestFactory, options, TracePropagation.Enabled, null, completionOption, cancellationToken);

    internal static async Task<HttpResponseMessage> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory,
        RetryOptions options, TracePropagation tracePropagation, ILogger? logger, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var traceId = ActivityTraceId.CreateRandom();
        while (true)
        {
            var (response, exception, sent) = await Attempt(client, requestFactory, tracePropagation, traceId, logger, completionOption, cancellationToken).ConfigureAwait(false);
            if (!ShouldRetry(response, exception, attempt, options))
                return response ?? throw exception!;

            attempt++;
            var delay = BackoffDelay(options, attempt);
            logger.RetryScheduled(sent.Method, sent.Uri, attempt, RetryReason(response, exception), delay);
            response?.Dispose();
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<(HttpResponseMessage? Response, HttpRequestException? Exception, SentRequest Sent)> Attempt(
        HttpClient client, Func<HttpRequestMessage> requestFactory, TracePropagation tracePropagation, ActivityTraceId traceId,
        ILogger? logger, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        TraceContext.Inject(request.Headers, tracePropagation, traceId);
        var sent = new SentRequest(request.Method, request.RequestUri!);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var response = await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
            logger.RequestCompleted(sent.Method, sent.Uri, (int) response.StatusCode, ElapsedSince(startTimestamp));
            return (response, null, sent);
        }
        catch (HttpRequestException e)
        {
            logger.RequestFailed(sent.Method, sent.Uri, e, ElapsedSince(startTimestamp));
            return (null, e, sent);
        }
    }

    private static TimeSpan ElapsedSince(long startTimestamp) =>
        TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - startTimestamp) / (double) Stopwatch.Frequency);

    private static string RetryReason(HttpResponseMessage? response, HttpRequestException? exception) =>
        exception is null ? ((int) response!.StatusCode).ToString(CultureInfo.InvariantCulture) : exception.GetType().Name;

    private sealed record SentRequest(HttpMethod Method, Uri Uri);

    private static bool ShouldRetry(HttpResponseMessage? response, HttpRequestException? exception, int attempt, RetryOptions options) =>
        attempt < options.MaxRetries && (exception != null || options.RetryableStatusCodes.Contains((int) response!.StatusCode));

    private static TimeSpan BackoffDelay(RetryOptions options, int attempt)
    {
        var exponentialMs = options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var cappedMs = Math.Min(exponentialMs, options.MaxDelay.TotalMilliseconds);
        var delayMs = options.UseJitter ? RandomProvider.Value!.NextDouble() * cappedMs : cappedMs;
        return TimeSpan.FromMilliseconds(delayMs);
    }
}
