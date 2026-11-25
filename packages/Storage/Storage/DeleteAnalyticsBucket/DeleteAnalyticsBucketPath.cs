using Supabase.Storage.Common;

namespace Supabase.Storage.DeleteAnalyticsBucket;

internal static class DeleteAnalyticsBucketPath
{
    internal static string For(string icebergUrl, string name) =>
        $"{icebergUrl}/bucket/{AnalyticsBucketName.Escape(name, nameof(name))}";
}
