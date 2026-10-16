namespace Supabase.CapabilityMatrix;

/// <summary>
/// The filesystem-backed <see cref="IWorkspace" /> — the imperative shell. Reads
/// SDK source under <c>packages/</c>, the canonical spec YAML files, and the
/// committed compliance file; writes the generated one.
/// </summary>
public sealed class FileWorkspace(string repositoryRoot, string specDirectory, string compliancePath, string outputPath)
    : IWorkspace
{
    public IReadOnlyList<SymbolWalker.SourceFile> ReadSources()
    {
        var packages = Path.Combine(repositoryRoot, "packages");
        if (!Directory.Exists(packages))
            return [];

        var sources = new List<SymbolWalker.SourceFile>();
        foreach (var path in Directory.EnumerateFiles(packages, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            if (SourceDiscovery.IsSdkSourceFile(relative))
                sources.Add(new SymbolWalker.SourceFile(relative, File.ReadAllText(path)));
        }

        return sources;
    }

    public IReadOnlyList<string> ReadSpecDocuments()
    {
        if (!Directory.Exists(specDirectory))
            throw new DirectoryNotFoundException($"Capability spec directory not found: {specDirectory}");

        return Directory.EnumerateFiles(specDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
    }

    public string ReadComplianceText() => File.ReadAllText(compliancePath);

    public void WriteComplianceText(string content) => File.WriteAllText(outputPath, content);
}
