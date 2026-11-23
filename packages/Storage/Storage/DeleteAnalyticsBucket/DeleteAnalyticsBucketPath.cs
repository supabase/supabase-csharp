using System;

namespace Supabase.Storage.DeleteAnalyticsBucket;

internal static class DeleteAnalyticsBucketPath
{
    private static readonly string[] RelativeSegments = { ".", ".." };

    internal static string For(string icebergUrl, string name)
    {
        RejectUnusable(name);
        return $"{icebergUrl}/bucket/{Uri.EscapeDataString(name)}";
    }

    private static void RejectUnusable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An analytics bucket name must be provided.", nameof(name));

        if (Array.IndexOf(RelativeSegments, name) >= 0)
            throw new ArgumentException("An analytics bucket name cannot be a relative path segment.", nameof(name));
    }
}
