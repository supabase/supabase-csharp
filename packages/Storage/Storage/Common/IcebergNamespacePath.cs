namespace Supabase.Storage.Common;

internal static class IcebergNamespacePath
{
    internal static string For(IcebergNamespace @namespace) =>
        PathSegment.Escape(@namespace.Joined(), "A namespace", nameof(@namespace));
}
