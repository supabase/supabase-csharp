namespace Supabase.CapabilityMatrix;

/// <summary>
/// Compares two normalized compliance models for semantic equivalence — same sdk,
/// same feature ids, and per feature the same status, note and set of symbols.
/// Formatting and symbol order are already normalized away. Pure.
/// </summary>
public static class ComplianceComparer
{
    /// <summary>Returns a human-readable difference per mismatch; empty when equivalent.</summary>
    public static IReadOnlyList<string> Diff(NormalizedCompliance expected, NormalizedCompliance actual)
    {
        var differences = new List<string>();

        if (expected.Sdk != actual.Sdk)
            differences.Add($"sdk: expected '{expected.Sdk}', got '{actual.Sdk}'");

        foreach (var key in expected.Features.Keys.Except(actual.Features.Keys).OrderBy(k => k, StringComparer.Ordinal))
            differences.Add($"{key}: present in expected, missing from generated");

        foreach (var key in actual.Features.Keys.Except(expected.Features.Keys).OrderBy(k => k, StringComparer.Ordinal))
            differences.Add($"{key}: generated but not in expected");

        foreach (var key in expected.Features.Keys.Intersect(actual.Features.Keys).OrderBy(k => k, StringComparer.Ordinal))
            differences.AddRange(DiffEntry(key, expected.Features[key], actual.Features[key]));

        return differences;
    }

    private static IEnumerable<string> DiffEntry(string key, NormalizedEntry expected, NormalizedEntry actual)
    {
        if (expected.Status != actual.Status)
            yield return $"{key}: status expected '{expected.Status}', got '{actual.Status}'";

        if (expected.Note != actual.Note)
            yield return $"{key}: note expected '{expected.Note ?? "<none>"}', got '{actual.Note ?? "<none>"}'";

        if (expected.NoPublicSymbols != actual.NoPublicSymbols)
            yield return $"{key}: no_public_symbols expected '{expected.NoPublicSymbols}', got '{actual.NoPublicSymbols}'";

        if (!expected.Symbols.SequenceEqual(actual.Symbols))
        {
            var missing = expected.Symbols.Except(actual.Symbols).ToList();
            var extra = actual.Symbols.Except(expected.Symbols).ToList();
            yield return $"{key}: symbols differ" +
                (missing.Count > 0 ? $" (missing: {string.Join(", ", missing)})" : string.Empty) +
                (extra.Count > 0 ? $" (extra: {string.Join(", ", extra)})" : string.Empty);
        }
    }
}
