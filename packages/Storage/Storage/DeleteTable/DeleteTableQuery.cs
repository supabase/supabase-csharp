namespace Supabase.Storage.DeleteTable;

internal static class DeleteTableQuery
{
    internal static string For(string tableUrl, DeleteTableOptions? options) =>
        options?.Purge is { } purge ? $"{tableUrl}?purgeRequested={WireValueOf(purge)}" : tableUrl;

    private static string WireValueOf(bool purge) => purge ? "true" : "false";
}
