using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Supabase.Storage.ListAnalyticsBuckets;

internal static class ListAnalyticsBucketsQuery
{
    internal static string For(string bucketUrl, ListAnalyticsBucketsOptions? options) =>
        $"{bucketUrl}?{string.Join("&", ParametersOf(options ?? new ListAnalyticsBucketsOptions()))}";

    private static IEnumerable<string> ParametersOf(ListAnalyticsBucketsOptions options)
    {
        if (options.Limit is { } limit)
            yield return Parameter("limit", limit.ToString(CultureInfo.InvariantCulture));

        if (options.Offset is { } offset)
            yield return Parameter("offset", offset.ToString(CultureInfo.InvariantCulture));

        if (options.Sort is { } sort)
        {
            yield return Parameter("sortColumn", WireNameOf(sort.Column));
            yield return Parameter("sortOrder", WireNameOf(sort.Direction));
        }

        if (options.Search is { } search)
            yield return Parameter("search", search);
    }

    private static string Parameter(string name, string value) => $"{name}={Uri.EscapeDataString(value)}";

    private static string WireNameOf(AnalyticsBucketSortColumn column) => column switch
    {
        AnalyticsBucketSortColumn.CreatedAt => "created_at",
        AnalyticsBucketSortColumn.UpdatedAt => "updated_at",
        AnalyticsBucketSortColumn.Name => "name",
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "The sort column is not one the service can sort analytics buckets by."),
    };

    private static string WireNameOf(ListSortDirection direction) =>
        direction == ListSortDirection.Descending ? "desc" : "asc";
}
