using Supabase.CapabilityMatrix.CanonicalMatrix;
using Supabase.CapabilityMatrix.Capabilities;

namespace Supabase.CapabilityMatrix.GeneratingTheComplianceFile;

/// <summary>
/// Renders the consolidated features into the <c>sdk-compliance.yaml</c> text: an
/// <c>sdk</c> header and a <c>features:</c> map laid out under per-area banners, in the
/// catalogue's order, with a blank line between groups and symbols listed one per line.
/// </summary>
public static class ComplianceFile
{
    private const int BannerWidth = 79;
    private static readonly string[] Header = ["sdk: csharp", "features:"];

    public static string For(IReadOnlyList<FeatureCompliance> features, FeatureCatalogue catalogue)
    {
        var byKey = features.ToDictionary(feature => feature.FeatureKey, StringComparer.Ordinal);
        var lines = Header.Concat(catalogue.Areas.SelectMany(area => AreaLines(area, byKey)));
        return string.Join('\n', lines) + '\n';
    }

    private static IEnumerable<string> AreaLines(CatalogueArea area, IReadOnlyDictionary<string, FeatureCompliance> byKey)
    {
        var groups = GroupedFeatureKeys(area)
            .Select(keys => keys.Where(byKey.ContainsKey).SelectMany(key => EntryLines(byKey[key])).ToList())
            .Where(block => block.Count > 0)
            .ToList();

        return groups.Count == 0
            ? []
            : [string.Empty, Banner(area.Title), .. groups.SelectMany(block => block.Prepend(string.Empty))];
    }

    private static IEnumerable<IReadOnlyList<string>> GroupedFeatureKeys(CatalogueArea area) =>
        area.Groups.Select(group => group.FeatureKeys).Append(area.UngroupedFeatureKeys);

    private static IEnumerable<string> EntryLines(FeatureCompliance feature) =>
    [
        $"  {feature.FeatureKey}:",
        $"    status: {feature.Status.InTheMatrix()}",
        .. feature.NoPublicSymbols ? ["    no_public_symbols: true"] : Array.Empty<string>(),
        .. string.IsNullOrEmpty(feature.Note) ? Array.Empty<string>() : [$"    note: \"{Escaped(feature.Note)}\""],
        .. feature.Symbols.Count == 0 ? Array.Empty<string>() : ["    symbols:", .. feature.Symbols.Select(symbol => $"      - {symbol}")],
    ];

    private static string Banner(string title)
    {
        var prefix = $"  # ── {title} ";
        return prefix + new string('─', Math.Max(0, BannerWidth - prefix.Length));
    }

    private static string Escaped(string note) =>
        note.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
