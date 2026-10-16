using System.Text;

namespace Supabase.CapabilityMatrix;

/// <summary>
/// Renders compliance entries to the exact <c>sdk-compliance.yaml</c> layout: an
/// <c>sdk</c> header, a <c>features:</c> map grouped under per-area banners, a blank
/// line between groups, and symbols listed one per line. Pure — a purpose-built
/// serializer rather than a YAML library so the banners and grouping are reproducible.
/// </summary>
public static class YamlEmitter
{
    private const string Sdk = "csharp";
    private const int BannerWidth = 79;

    public static string Emit(IReadOnlyList<ComplianceEntry> entries, SpecModel spec)
    {
        var byKey = entries.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        var lines = new List<string> { $"sdk: {Sdk}", "features:" };

        foreach (var area in spec.Areas)
        {
            var groupBlocks = GroupBlocks(area, byKey).ToList();
            if (groupBlocks.Count == 0)
                continue;

            lines.Add(string.Empty);
            lines.Add(Banner(area.Title));
            for (var index = 0; index < groupBlocks.Count; index++)
            {
                lines.Add(string.Empty);
                lines.AddRange(groupBlocks[index]);
            }
        }

        return string.Join('\n', lines) + '\n';
    }

    private static IEnumerable<IReadOnlyList<string>> GroupBlocks(
        AreaSpec area,
        IReadOnlyDictionary<string, ComplianceEntry> byKey)
    {
        var featureGroups = area.Groups
            .Select(group => group.FeatureIds)
            .Append(area.UngroupedFeatureIds);

        foreach (var featureIds in featureGroups)
        {
            var block = featureIds
                .Where(byKey.ContainsKey)
                .SelectMany(id => EntryLines(byKey[id]))
                .ToList();
            if (block.Count > 0)
                yield return block;
        }
    }

    private static IEnumerable<string> EntryLines(ComplianceEntry entry)
    {
        yield return $"  {entry.Key}:";
        yield return $"    status: {entry.Status.ToYamlValue()}";
        if (entry.NoPublicSymbols)
            yield return "    no_public_symbols: true";
        if (!string.IsNullOrEmpty(entry.Note))
            yield return $"    note: \"{Escape(entry.Note)}\"";
        if (entry.Symbols.Count > 0)
        {
            yield return "    symbols:";
            foreach (var symbol in entry.Symbols)
                yield return $"      - {symbol}";
        }
    }

    private static string Banner(string title)
    {
        var prefix = $"  # ── {title} ";
        var builder = new StringBuilder(prefix);
        while (builder.Length < BannerWidth)
            builder.Append('─');
        return builder.ToString();
    }

    private static string Escape(string note) =>
        note.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
