namespace Supabase.CapabilityMatrix.Capabilities;

/// <summary>
/// One feature's compliance, consolidated from every member that declares it: the
/// status, the note, and the public symbols that evidence it. When only non-public
/// members declare it there is no symbol to cite, which the matrix records as
/// <c>no_public_symbols</c>.
/// </summary>
public sealed record FeatureCompliance(
    string FeatureKey,
    CapabilityStatus Status,
    string? Note,
    IReadOnlyList<string> Symbols,
    bool NoPublicSymbols)
{
    /// <summary>
    /// Folds the scattered declarations into one entry per feature, ordered by key.
    /// Partial wins a mixed status (the conservative reading); a conflict between
    /// declarations is a validation concern, not this projection's to resolve.
    /// </summary>
    public static IReadOnlyList<FeatureCompliance> Consolidate(IEnumerable<DeclaredCapability> declarations) =>
        declarations
            .GroupBy(declaration => declaration.FeatureKey, StringComparer.Ordinal)
            .OrderBy(feature => feature.Key, StringComparer.Ordinal)
            .Select(ForOneFeature)
            .ToList();

    private static FeatureCompliance ForOneFeature(IGrouping<string, DeclaredCapability> feature)
    {
        var symbols = feature
            .Where(declaration => declaration.FromPublicApi)
            .Select(declaration => declaration.Symbol)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(symbol => symbol, StringComparer.Ordinal)
            .ToList();

        var status = feature.Any(declaration => declaration.Status is CapabilityStatus.Partial)
            ? CapabilityStatus.Partial
            : CapabilityStatus.Implemented;

        var note = feature
            .Select(declaration => declaration.Note)
            .FirstOrDefault(note => !string.IsNullOrEmpty(note));

        return new FeatureCompliance(feature.Key, status, note, symbols, NoPublicSymbols: symbols.Count == 0);
    }
}
