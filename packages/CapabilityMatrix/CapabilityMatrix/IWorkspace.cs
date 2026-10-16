namespace Supabase.CapabilityMatrix;

/// <summary>
/// The I/O seam for the commands: everything that touches the filesystem goes
/// through here, so command logic stays pure and testable with an in-memory
/// workspace. The imperative shell (<see cref="FileWorkspace" />) is the only
/// implementation that reads or writes disk.
/// </summary>
public interface IWorkspace
{
    /// <summary>The SDK's own source files, as (repo-relative path, text).</summary>
    IReadOnlyList<SymbolWalker.SourceFile> ReadSources();

    /// <summary>The canonical capability-area YAML documents.</summary>
    IReadOnlyList<string> ReadSpecDocuments();

    /// <summary>The current committed <c>sdk-compliance.yaml</c> text.</summary>
    string ReadComplianceText();

    /// <summary>Writes the generated <c>sdk-compliance.yaml</c> text.</summary>
    void WriteComplianceText(string content);
}
