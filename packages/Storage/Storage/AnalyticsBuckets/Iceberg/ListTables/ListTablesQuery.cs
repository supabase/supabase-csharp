using System.Collections.Generic;
using System.Globalization;
using Supabase.Storage.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListTables;

internal static class ListTablesQuery
{
    internal static string For(string tablesUrl, ListTablesOptions? options) =>
        QueryString.Append(tablesUrl, ParametersOf(options ?? new ListTablesOptions()));

    private static IEnumerable<string> ParametersOf(ListTablesOptions options)
    {
        if (options.PageToken is { } pageToken)
            yield return QueryString.Parameter("pageToken", pageToken);

        if (options.PageSize is { } pageSize)
            yield return QueryString.Parameter("pageSize", pageSize.ToString(CultureInfo.InvariantCulture));
    }
}
