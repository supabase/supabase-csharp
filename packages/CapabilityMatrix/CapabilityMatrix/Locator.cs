namespace Supabase.CapabilityMatrix;

/// <summary>Where a compliance symbol was found (or not) in the SDK source.</summary>
public sealed record SymbolLocation(string Key, string Symbol, string FilePath, int Line);

/// <summary>A compliance symbol that resolves to more than one declaration.</summary>
public sealed record AmbiguousSymbol(string Key, string Symbol, IReadOnlyList<SymbolLocation> Candidates);

/// <summary>A compliance symbol with no matching declaration in the source.</summary>
public sealed record UnresolvedSymbol(string Key, string Symbol);

/// <summary>The result of resolving every symbol in a compliance file against the source.</summary>
public sealed record LocateReport(
    IReadOnlyList<SymbolLocation> Resolved,
    IReadOnlyList<AmbiguousSymbol> Ambiguous,
    IReadOnlyList<UnresolvedSymbol> Unresolved);

/// <summary>
/// Resolves each <c>symbols</c> entry of an existing compliance file to its
/// declaration(s) in the extracted source index. Drives the one-off annotation
/// pass and exposes stale or mistyped symbols in the hand-written file. Pure.
/// </summary>
public static class Locator
{
    public static LocateReport Locate(NormalizedCompliance compliance, IReadOnlyList<DeclaredSymbol> symbols)
    {
        var byName = symbols
            .GroupBy(symbol => symbol.QualifiedName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var resolved = new List<SymbolLocation>();
        var ambiguous = new List<AmbiguousSymbol>();
        var unresolved = new List<UnresolvedSymbol>();

        var requested = compliance.Features
            .SelectMany(feature => feature.Value.Symbols.Select(symbol => (Key: feature.Key, Symbol: symbol)))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Symbol, StringComparer.Ordinal);

        foreach (var (key, symbol) in requested)
        {
            if (!byName.TryGetValue(symbol, out var matches))
            {
                unresolved.Add(new UnresolvedSymbol(key, symbol));
                continue;
            }

            var locations = matches
                .Select(match => new SymbolLocation(key, symbol, match.FilePath, match.Line))
                .ToList();

            var distinctFiles = matches.Select(match => match.FilePath).Distinct(StringComparer.Ordinal).Count();
            if (distinctFiles > 1)
                ambiguous.Add(new AmbiguousSymbol(key, symbol, locations));
            else
                resolved.Add(locations[0]);
        }

        return new LocateReport(resolved, ambiguous, unresolved);
    }
}
