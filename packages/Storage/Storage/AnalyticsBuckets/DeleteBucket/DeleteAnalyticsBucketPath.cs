using Supabase.Storage.AnalyticsBuckets.Common;

namespace Supabase.Storage.AnalyticsBuckets.DeleteBucket;

internal static class DeleteAnalyticsBucketPath
{
    internal static string For(string icebergUrl, string name) =>
        $"{icebergUrl}/bucket/{AnalyticsBucketName.Escape(name, nameof(name))}";
}
