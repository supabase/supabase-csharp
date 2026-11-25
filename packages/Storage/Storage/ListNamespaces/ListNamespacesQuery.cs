using System;
using System.Collections.Generic;
using System.Globalization;

namespace Supabase.Storage.ListNamespaces;

internal static class ListNamespacesQuery
{
    internal static string For(string namespacesUrl, ListNamespacesOptions? options) =>
        $"{namespacesUrl}?{string.Join("&", ParametersOf(options ?? new ListNamespacesOptions()))}";

    private static IEnumerable<string> ParametersOf(ListNamespacesOptions options)
    {
        if (options.Parent is { } parent)
            yield return Parameter("parent", parent.Joined());

        if (options.PageToken is { } pageToken)
            yield return Parameter("pageToken", pageToken);

        if (options.PageSize is { } pageSize)
            yield return Parameter("pageSize", pageSize.ToString(CultureInfo.InvariantCulture));
    }

    private static string Parameter(string name, string value) => $"{name}={Uri.EscapeDataString(value)}";
}
