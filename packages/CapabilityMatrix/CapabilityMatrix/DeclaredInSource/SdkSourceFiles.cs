namespace Supabase.CapabilityMatrix.DeclaredInSource;

/// <summary>
/// Which repository files make up the SDK's own public source. A function of the
/// repo-relative path alone, so it needs no filesystem to decide.
/// </summary>
public static class SdkSourceFiles
{
    private static readonly string[] NotSource = ["bin", "obj"];

    /// <summary>
    /// A production SDK C# file: under <c>packages/</c>, not a build output, not test
    /// code, and not the capability-matrix tool itself.
    /// </summary>
    public static bool Includes(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        return relativePath.EndsWith(".cs", StringComparison.Ordinal)
            && segments is ["packages", var package, ..] && package != "CapabilityMatrix"
            && !segments.Any(segment => NotSource.Contains(segment) || segment.EndsWith(".Tests", StringComparison.Ordinal));
    }
}
