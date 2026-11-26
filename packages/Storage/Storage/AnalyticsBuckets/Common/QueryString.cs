using System;
using System.Collections.Generic;

namespace Supabase.Storage.AnalyticsBuckets.Common;

internal static class QueryString
{
    internal static string Append(string url, IEnumerable<string> parameters) => $"{url}?{string.Join("&", parameters)}";

    internal static string Parameter(string name, string value) => $"{name}={Uri.EscapeDataString(value)}";
}
