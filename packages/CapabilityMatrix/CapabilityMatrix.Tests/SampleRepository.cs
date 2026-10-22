namespace Supabase.CapabilityMatrix.Tests;

/// <summary>
/// A throwaway on-disk repository — one annotated SDK file and one capability-area spec,
/// both from <c>Data/</c> — for exercising the command line end to end. Makes itself the
/// working directory for its lifetime (the tool reads <c>./packages</c>), then deletes itself.
/// </summary>
internal sealed class SampleRepository : IDisposable
{
    private readonly string previousDirectory;

    public string Root { get; }
    public string Spec { get; }
    public string Output => Path.Combine(this.Root, "sdk-compliance.yaml");

    private SampleRepository(string root)
    {
        this.previousDirectory = Directory.GetCurrentDirectory();
        this.Root = root;
        this.Spec = Path.Combine(root, "spec");
        Directory.SetCurrentDirectory(root);
    }

    public static SampleRepository Create() => From("repo-source.cs.txt");

    /// <summary>A repository whose source declares a feature the catalogue does not know.</summary>
    public static SampleRepository Drifted() => From("drifted-source.cs.txt");

    private static SampleRepository From(string sourceFixture)
    {
        var root = Directory.CreateTempSubdirectory("capability-matrix").FullName;
        var package = Directory.CreateDirectory(Path.Combine(root, "packages", "Gotrue", "Gotrue"));
        File.WriteAllText(Path.Combine(package.FullName, "Client.cs"), Fixtures.Read(sourceFixture));
        var spec = Directory.CreateDirectory(Path.Combine(root, "spec"));
        File.WriteAllText(Path.Combine(spec.FullName, "auth.yaml"), Fixtures.Read("sample-area.yaml"));
        return new SampleRepository(root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(this.previousDirectory);
        Directory.Delete(this.Root, recursive: true);
    }
}
