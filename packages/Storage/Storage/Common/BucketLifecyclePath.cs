using System;

namespace Supabase.Storage.Common;

internal static class BucketLifecyclePath
{
    internal static string For(string storageUrl, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or "..")
            throw new ArgumentException("A bucket id must be provided and cannot be a relative path segment.", nameof(id));

        return $"{storageUrl}/bucket/{Uri.EscapeDataString(id)}/lifecycle";
    }
}
