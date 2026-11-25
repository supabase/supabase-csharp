using Supabase.Storage.Common;

namespace Supabase.Storage.NamespaceExists;

internal static class IcebergNamespacePath
{
    internal static string For(IcebergNamespace @namespace) =>
        PathSegment.Escape(@namespace.Joined(), "A namespace", nameof(@namespace));
}
