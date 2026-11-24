using System;
using System.Linq;
using Supabase.Core.Diagnostics;

namespace Supabase.Postgrest;

/// <summary>
/// The form of a Postgrest request URL that is safe to record in telemetry.
///
/// Postgrest carries column filters and their values in the query string
/// (<c>email=eq.john@doe.com</c>), so every query value is replaced by <c>REDACTED</c> except
/// for the keys that only shape the result (<c>select</c>, <c>order</c>, <c>limit</c>,
/// <c>offset</c>). Keys are kept. User info and fragment are dropped.
/// </summary>
internal static class RedactedUrl
{
    private const string Redacted = "REDACTED";

    private static readonly string[] SafeKeys = { "select", "order", "limit", "offset" };

    /// <summary>
    /// Returns the URL with its query values redacted.
    /// </summary>
    /// <param name="uri">The request URI.</param>
    internal static string Of(Uri uri)
    {
        var query = uri.Query.TrimStart('?');
        var url = UrlSanitizer.Sanitize(uri);
        return query.Length == 0 ? url : $"{url}?{RedactQuery(query)}";
    }

    private static string RedactQuery(string query) =>
        string.Join("&", query.Split('&').Select(RedactPair));

    private static string RedactPair(string pair)
    {
        var separator = pair.IndexOf('=');
        var key = separator < 0 ? pair : pair.Substring(0, separator);
        return IsSafe(key) && separator >= 0 ? pair : $"{key}={Redacted}";
    }

    private static bool IsSafe(string key) =>
        SafeKeys.Contains(Uri.UnescapeDataString(key), StringComparer.Ordinal);
}
