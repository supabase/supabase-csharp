using System;
using System.Linq;

namespace Supabase.Gotrue.CustomProviders;

internal static class CustomProviderIdentifier
{
    private const string Prefix = "custom:";

    private static readonly char[] RouteChangingCharacters = { '/', '\\', '?', '#', '%' };

    internal static string EnsureRoutable(string identifier)
    {
        if (identifier is null)
            throw new ArgumentNullException(nameof(identifier));
        if (!identifier.StartsWith(Prefix, StringComparison.Ordinal))
            throw new ArgumentException($"Custom provider identifiers start with '{Prefix}', for example '{Prefix}mycompany'.", nameof(identifier));
        if (identifier.Any(RouteChangingCharacters.Contains))
            throw new ArgumentException("Custom provider identifiers cannot contain '/', '\\', '?', '#' or '%': they would change the route of the request.", nameof(identifier));
        return identifier;
    }
}
