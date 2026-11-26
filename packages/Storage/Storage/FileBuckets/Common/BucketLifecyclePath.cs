using Supabase.Storage.Common;

namespace Supabase.Storage.FileBuckets.Common;

internal static class BucketLifecyclePath
{
    internal static string For(string storageUrl, string id) =>
        $"{storageUrl}/bucket/{PathSegment.Escape(id, "A bucket id", nameof(id))}/lifecycle";
}
