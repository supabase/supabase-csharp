using System.Collections.Generic;

namespace Supabase.Postgrest.Common;

internal static class PreferHeader
{
    private const string Name = "Prefer";

    internal static void Append(Dictionary<string, string> headers, string preference) =>
        headers[Name] = headers.TryGetValue(Name, out var preferences) ? $"{preferences},{preference}" : preference;
}
