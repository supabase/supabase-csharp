using System.Collections.Generic;
using System.Globalization;
using Supabase.Storage.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListNamespaces;

internal static class ListNamespacesQuery
{
    internal static string For(string namespacesUrl, ListNamespacesOptions? options) =>
        QueryString.Append(namespacesUrl, ParametersOf(options ?? new ListNamespacesOptions()));

    private static IEnumerable<string> ParametersOf(ListNamespacesOptions options)
    {
        if (options.Parent is { } parent)
            yield return QueryString.Parameter("parent", parent.Joined());

        if (options.PageToken is { } pageToken)
            yield return QueryString.Parameter("pageToken", pageToken);

        if (options.PageSize is { } pageSize)
            yield return QueryString.Parameter("pageSize", pageSize.ToString(CultureInfo.InvariantCulture));
    }
}
