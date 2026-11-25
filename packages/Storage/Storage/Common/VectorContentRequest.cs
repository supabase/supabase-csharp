namespace Supabase.Storage.Common;

internal static class VectorContentRequest
{
    internal static bool? RequestedOrOmitted(VectorContent include, VectorContent content) =>
        (include & content) == content ? true : null;
}
