using YamlDotNet.RepresentationModel;

namespace Supabase.CapabilityMatrix;

/// <summary>A feature entry read from an existing <c>sdk-compliance.yaml</c>, normalized for comparison.</summary>
/// <param name="Status">The status string, verbatim.</param>
/// <param name="Note">The note, or null when absent.</param>
/// <param name="Symbols">The <c>symbols</c> list, sorted for order-independent comparison.</param>
/// <param name="NoPublicSymbols">The <c>no_public_symbols</c> flag.</param>
public sealed record NormalizedEntry(string Status, string? Note, IReadOnlyList<string> Symbols, bool NoPublicSymbols);

/// <summary>A whole compliance file reduced to the facts the matrix cares about.</summary>
public sealed record NormalizedCompliance(string Sdk, IReadOnlyDictionary<string, NormalizedEntry> Features);

/// <summary>
/// Reads an existing <c>sdk-compliance.yaml</c> into a normalized model, and
/// projects the generator's own entries into the same shape, so the two can be
/// compared for semantic equivalence independent of formatting or symbol order.
/// </summary>
public static class ComplianceYaml
{
    public static NormalizedCompliance Parse(string text)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(text);
        stream.Load(reader);

        var root = (YamlMappingNode) stream.Documents[0].RootNode;
        var sdk = Scalar(root, "sdk") ?? string.Empty;

        var features = new Dictionary<string, NormalizedEntry>(StringComparer.Ordinal);
        if (TryGet(root, "features") is YamlMappingNode featureMap)
        {
            foreach (var (keyNode, valueNode) in featureMap.Children)
            {
                var key = ((YamlScalarNode) keyNode).Value!;
                features[key] = ReadEntry(valueNode);
            }
        }

        return new NormalizedCompliance(sdk, features);
    }

    /// <summary>Projects generated entries into the normalized shape used for comparison.</summary>
    public static NormalizedCompliance FromEntries(IReadOnlyList<ComplianceEntry> entries) =>
        new("csharp", entries.ToDictionary(
            entry => entry.Key,
            entry => new NormalizedEntry(
                entry.Status.ToYamlValue(),
                string.IsNullOrEmpty(entry.Note) ? null : entry.Note,
                entry.Symbols.OrderBy(s => s, StringComparer.Ordinal).ToList(),
                entry.NoPublicSymbols),
            StringComparer.Ordinal));

    private static NormalizedEntry ReadEntry(YamlNode valueNode)
    {
        if (valueNode is YamlScalarNode scalar)
            return new NormalizedEntry(scalar.Value ?? string.Empty, null, [], false);

        var map = (YamlMappingNode) valueNode;
        var status = Scalar(map, "status") ?? string.Empty;
        var note = Scalar(map, "note");
        var noPublicSymbols = string.Equals(Scalar(map, "no_public_symbols"), "true", StringComparison.Ordinal);
        var symbols = TryGet(map, "symbols") is YamlSequenceNode sequence
            ? sequence.Children.OfType<YamlScalarNode>().Select(n => n.Value!).OrderBy(s => s, StringComparer.Ordinal).ToList()
            : [];
        return new NormalizedEntry(status, note, symbols, noPublicSymbols);
    }

    private static string? Scalar(YamlMappingNode map, string key) =>
        TryGet(map, key) is YamlScalarNode scalar ? scalar.Value : null;

    private static YamlNode? TryGet(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;
}
