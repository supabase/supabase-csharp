namespace Supabase.Storage.Common;

internal static class AnalyticsBucketName
{
    internal static string Escape(string name, string paramName) =>
        PathSegment.Escape(name, "An analytics bucket name", paramName);
}
