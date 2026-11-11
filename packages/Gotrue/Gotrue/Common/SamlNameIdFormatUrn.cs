namespace Supabase.Gotrue.Common;

internal static class SamlNameIdFormatUrn
{
    internal static string? Of(SamlNameIdFormat? format) =>
        format.HasValue ? Core.Helpers.GetMappedToAttr(format.Value).Mapping : null;
}
