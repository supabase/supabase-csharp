using System.Collections.Generic;
using System.Globalization;
using Supabase.Storage.AnalyticsBuckets.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListNamespaces;

internal static class ListNamespacesQuery
{
    internal static string For(string namespacesUrl, ListNamespacesOptions? options) =>
        QueryString.Append(namespacesUrl, ParametersOf(options ?? new ListNamespacesOptions()));

    private static IEnumerable<string> ParametersOf(ListNamespacesOptions options)
    {
        if (options.Parent is { } parent)
            yield return QueryString.Parameter("parent", parent.Joined());

        if (options.NextPageToken is { } nextPageToken)
            yield return QueryString.Parameter("pageToken", nextPageToken);

        if (options.PageSize is { } pageSize)
            yield return QueryString.Parameter("pageSize", pageSize.ToString(CultureInfo.InvariantCulture));
    }
}
