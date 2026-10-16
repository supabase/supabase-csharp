namespace Supabase.CapabilityMatrix;

/// <summary>
/// Decides which repository files make up the SDK's own source. Pure: the rule is a
/// function of the repo-relative path, so it is unit-testable without a filesystem.
/// </summary>
public static class SourceDiscovery
{
    private static readonly string[] ExcludedSegments = ["bin", "obj"];

    /// <summary>
    /// True for a production SDK C# file: under <c>packages/</c>, not in a build output
    /// directory, not test code, and not the capability-matrix tool itself.
    /// </summary>
    public static bool IsSdkSourceFile(string relativePath)
    {
        if (!relativePath.EndsWith(".cs", StringComparison.Ordinal))
            return false;

        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "packages")
            return false;

        if (segments[1] == "CapabilityMatrix")
            return false;

        return !segments.Any(segment =>
            ExcludedSegments.Contains(segment) || segment.EndsWith(".Tests", StringComparison.Ordinal));
    }
}
