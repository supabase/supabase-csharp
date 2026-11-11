using System.Collections.Generic;

namespace Supabase.Gotrue.ListSsoProviders;

internal static class ListSsoProvidersQuery
{
    internal static Dictionary<string, string>? For(SsoProviderFilter? filter) =>
        filter is { } only ? new Dictionary<string, string> { [only.Parameter] = only.Value } : null;
}
