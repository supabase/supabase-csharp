using System.Collections.Generic;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.ListCustomProviders;

internal static class ListCustomProvidersQuery
{
    internal static Dictionary<string, string>? For(CustomProviderType? type) =>
        type is { } filter ? new Dictionary<string, string> { ["type"] = Core.Helpers.GetMappedToAttr(filter).Mapping } : null;
}
