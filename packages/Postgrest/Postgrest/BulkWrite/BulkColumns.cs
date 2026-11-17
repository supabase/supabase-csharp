using System.Collections.Generic;
using System.Linq;

namespace Supabase.Postgrest.BulkWrite;

internal static class BulkColumns
{
    internal static IReadOnlyList<string> UnionOf(object? requestBody) =>
        RowsOf(requestBody)
            .OfType<IDictionary<string, object?>>()
            .SelectMany(row => row.Keys)
            .Distinct()
            .Select(Quote)
            .ToList();

    private static IEnumerable<object> RowsOf(object? requestBody) =>
        requestBody as List<object> ?? Enumerable.Empty<object>();

    private static string Quote(string column) => $"\"{column}\"";
}
