using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Supabase.Core.Diagnostics;

namespace Supabase.Core.Logging;

internal static class SdkLog
{
    private static readonly Action<ILogger, string, string, int, double, Exception?> Completed =
        LoggerMessage.Define<string, string, int, double>(LogLevel.Debug, new EventId(1, nameof(RequestCompleted)),
            "{Method} {Url} responded {StatusCode} in {ElapsedMs} ms");

    private static readonly Action<ILogger, string, string, string, double, Exception?> Failed =
        LoggerMessage.Define<string, string, string, double>(LogLevel.Debug, new EventId(2, nameof(RequestFailed)),
            "{Method} {Url} failed with {ErrorType} after {ElapsedMs} ms");

    private static readonly Action<ILogger, string, string, int, string, double, Exception?> Retrying =
        LoggerMessage.Define<string, string, int, string, double>(LogLevel.Debug, new EventId(3, nameof(RetryScheduled)),
            "{Method} {Url} will be retried (attempt {Attempt}) after {Reason} in {DelayMs} ms");

    private static readonly Action<ILogger, string, string, Exception?> BackgroundEvent =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(4, nameof(BackgroundWorkEvent)), "{Component}: {Event}");

    private static readonly Action<ILogger, string, string, string, Exception?> BackgroundFailure =
        LoggerMessage.Define<string, string, string>(LogLevel.Warning, new EventId(5, nameof(BackgroundWorkFailed)),
            "{Component}: {Operation} failed with {ErrorType}");

    internal static void RequestCompleted(this ILogger? logger, HttpMethod method, Uri uri, int statusCode, TimeSpan elapsed)
    {
        if (logger is null)
            return;
        TryWrite(logger, LogLevel.Debug, () => Completed(logger, method.Method, UrlSanitizer.Sanitize(uri), statusCode, elapsed.TotalMilliseconds, null));
    }

    internal static void RequestFailed(this ILogger? logger, HttpMethod method, Uri uri, Exception error, TimeSpan elapsed)
    {
        if (logger is null)
            return;
        TryWrite(logger, LogLevel.Debug, () => Failed(logger, method.Method, UrlSanitizer.Sanitize(uri), error.GetType().Name, elapsed.TotalMilliseconds, null));
    }

    internal static void RetryScheduled(this ILogger? logger, HttpMethod method, Uri uri, int attempt, string reason, TimeSpan delay)
    {
        if (logger is null)
            return;
        TryWrite(logger, LogLevel.Debug, () => Retrying(logger, method.Method, UrlSanitizer.Sanitize(uri), attempt, reason, delay.TotalMilliseconds, null));
    }

    internal static void BackgroundWorkEvent(this ILogger? logger, string component, string description)
    {
        if (logger is null)
            return;
        TryWrite(logger, LogLevel.Debug, () => BackgroundEvent(logger, component, description, null));
    }

    internal static void BackgroundWorkFailed(this ILogger? logger, string component, string operation, Exception error)
    {
        if (logger is null)
            return;
        TryWrite(logger, LogLevel.Warning, () => BackgroundFailure(logger, component, operation, error.GetType().Name, null));
    }

    private static void TryWrite(ILogger logger, LogLevel level, Action write)
    {
        try
        {
            if (logger.IsEnabled(level))
                write();
        }
        catch (Exception)
        {
        }
    }
}
