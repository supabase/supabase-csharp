using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Supabase.Core.Http;

/// <summary>Sends a request, retrying with backoff per <see cref="RetryOptions"/>. Default options send once, un-retried.</summary>
public static class RetryExecutor
{
    private static readonly ThreadLocal<Random> RandomProvider = new(() => new Random(Guid.NewGuid().GetHashCode()));

    /// <summary>Sends via <paramref name="requestFactory"/>, called fresh per attempt since a sent <see cref="HttpRequestMessage"/> can't be reused.</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory,
        RetryOptions options, CancellationToken cancellationToken = default) =>
        SendAsync(client, requestFactory, options, HttpCompletionOption.ResponseContentRead, cancellationToken);

    /// <summary>Sends a request with the specified <paramref name="completionOption"/>.</summary>
    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, Func<HttpRequestMessage> requestFactory,
        RetryOptions options, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            var (response, exception) = await Attempt(client, requestFactory, completionOption, cancellationToken).ConfigureAwait(false);
            if (!ShouldRetry(response, exception, attempt, options))
                return response ?? throw exception!;

            response?.Dispose();
            attempt++;
            await Task.Delay(BackoffDelay(options, attempt), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<(HttpResponseMessage? Response, HttpRequestException? Exception)> Attempt(
        HttpClient client, Func<HttpRequestMessage> requestFactory, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        try
        {
            return (await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false), null);
        }
        catch (HttpRequestException e)
        {
            return (null, e);
        }
    }

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
