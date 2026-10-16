namespace Supabase.CapabilityMatrix;

/// <summary>One generated <c>sdk-compliance.yaml</c> feature entry.</summary>
/// <param name="Key">The capability-matrix feature id.</param>
/// <param name="Status">The reported status.</param>
/// <param name="Note">The note, if any.</param>
/// <param name="Symbols">The implementing public symbols, sorted and de-duplicated.</param>
/// <param name="NoPublicSymbols">
/// True when the feature is implemented only through non-public API, so there is no
/// public symbol to list. Mirrors the matrix's <c>no_public_symbols</c> flag.
/// </param>
public sealed record ComplianceEntry(
    string Key,
    CapabilityStatus Status,
    string? Note,
    IReadOnlyList<string> Symbols,
    bool NoPublicSymbols = false);

/// <summary>A problem found while aggregating annotations into compliance entries.</summary>
/// <param name="Message">Human-readable description, surfaced on CI.</param>
public sealed record ValidationError(string Message);

/// <summary>The aggregated compliance entries plus any errors found building them.</summary>
public sealed record ComplianceResult(
    IReadOnlyList<ComplianceEntry> Entries,
    IReadOnlyList<ValidationError> Errors);

/// <summary>
/// Aggregates the capability annotations scattered across declared symbols into one
/// entry per feature id. Pure. Two members tagged with the same key contribute two
/// symbols to one entry; the same member may carry several keys.
/// </summary>
public static class ComplianceModel
{
    public static ComplianceResult Build(IReadOnlyList<DeclaredSymbol> symbols)
    {
        var errors = new List<ValidationError>();

        var annotated = symbols
            .SelectMany(symbol => symbol.Annotations.Select(annotation => (symbol, annotation)))
            .ToList();

        var entries = new List<ComplianceEntry>();
        foreach (var group in annotated.GroupBy(pair => pair.annotation.Key).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var statuses = group.Select(pair => pair.annotation.Status).Distinct().ToList();
            if (statuses.Count > 1)
            {
                errors.Add(new ValidationError(
                    $"'{group.Key}' is reported with conflicting statuses (implemented and partially_implemented) " +
                    "across members — a feature has a single status."));
            }

            var notes = group
                .Select(pair => pair.annotation.Note)
                .Where(note => !string.IsNullOrEmpty(note))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(note => note, StringComparer.Ordinal)
                .ToList();
            if (notes.Count > 1)
            {
                errors.Add(new ValidationError(
                    $"'{group.Key}' carries {notes.Count} differing notes across members — a feature has a single note."));
            }

            var symbolNames = group
                .Where(pair => pair.symbol.IsPublic)
                .Select(pair => pair.symbol.QualifiedName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            // Partial wins a status conflict (the conservative reading); it is reported
            // as an error above regardless, so a clean tree never relies on this choice.
            var status = statuses.Contains(CapabilityStatus.PartiallyImplemented)
                ? CapabilityStatus.PartiallyImplemented
                : CapabilityStatus.Implemented;

            // A feature implemented only through non-public API has no public symbol to
            // cite; the matrix records that with no_public_symbols, which requires a note.
            var noPublicSymbols = symbolNames.Count == 0;
            var note = notes.FirstOrDefault();
            if (noPublicSymbols && string.IsNullOrEmpty(note))
            {
                errors.Add(new ValidationError(
                    $"'{group.Key}' is implemented only through non-public API and needs a note " +
                    "explaining how the capability is exposed (matrix: no_public_symbols requires a note)."));
            }

            entries.Add(new ComplianceEntry(group.Key, status, note, symbolNames, noPublicSymbols));
        }

        return new ComplianceResult(entries, errors);
    }
}
