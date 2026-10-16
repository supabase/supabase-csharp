namespace Supabase.CapabilityMatrix;

/// <summary>
/// Checks that every capability key used in the source is a real feature id from
/// the canonical spec — the typo guard run in-PR. Pure.
/// </summary>
public static class KeyValidator
{
    public static IReadOnlyList<ValidationError> ValidateKeys(
        IReadOnlyList<ComplianceEntry> entries,
        IReadOnlySet<string> knownIds)
    {
        return entries
            .Where(entry => !knownIds.Contains(entry.Key))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ValidationError(
                $"'{entry.Key}' is not a known capability-matrix feature id " +
                $"(used by: {string.Join(", ", entry.Symbols)}). Check for a typo against the canonical spec."))
            .ToList();
    }
}
