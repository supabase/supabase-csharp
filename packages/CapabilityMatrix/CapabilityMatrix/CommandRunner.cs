using System.Text.Json;

namespace Supabase.CapabilityMatrix;

/// <summary>
/// The four commands — generate, validate, locate, check — operating over an
/// <see cref="IWorkspace" />. All business logic lives in the pure classes it
/// composes; this layer only loads inputs, reports, and chooses an exit code.
/// </summary>
public sealed class CommandRunner
{
    public const int Success = 0;
    public const int Failure = 1;

    private static readonly JsonSerializerOptions IndexJsonOptions = new() { WriteIndented = true };

    private readonly IWorkspace workspace;
    private readonly TextWriter output;

    public CommandRunner(IWorkspace workspace, TextWriter output)
    {
        this.workspace = workspace;
        this.output = output;
    }

    /// <summary>Regenerates and writes <c>sdk-compliance.yaml</c>, failing on any authoring error.</summary>
    public int Generate()
    {
        var (result, spec, errors) = this.Analyze();
        if (errors.Count > 0)
        {
            this.ReportErrors("Cannot generate sdk-compliance.yaml", errors);
            return Failure;
        }

        this.workspace.WriteComplianceText(YamlEmitter.Emit(result.Entries, spec));
        this.output.WriteLine($"✅ Generated sdk-compliance.yaml ({result.Entries.Count} features).");
        return Success;
    }

    /// <summary>Validates the capability annotations without touching the committed file.</summary>
    public int Validate()
    {
        var (_, _, errors) = this.Analyze();
        if (errors.Count > 0)
        {
            this.ReportErrors("Capability annotation check failed", errors);
            return Failure;
        }

        this.output.WriteLine("✅ Capability annotations are valid.");
        return Success;
    }

    /// <summary>Resolves every symbol in the committed file against the source and reports the result.</summary>
    public int Locate()
    {
        var symbols = SymbolWalker.Extract(this.workspace.ReadSources());
        var compliance = ComplianceYaml.Parse(this.workspace.ReadComplianceText());
        var report = Locator.Locate(compliance, symbols);

        foreach (var location in report.Resolved)
            this.output.WriteLine($"  ok    {location.Key} → {location.Symbol} ({location.FilePath}:{location.Line})");
        foreach (var ambiguous in report.Ambiguous)
            this.output.WriteLine($"  AMBIG {ambiguous.Key} → {ambiguous.Symbol} in {ambiguous.Candidates.Count} files: " +
                string.Join(", ", ambiguous.Candidates.Select(c => $"{c.FilePath}:{c.Line}")));
        foreach (var unresolved in report.Unresolved)
            this.output.WriteLine($"  MISS  {unresolved.Key} → {unresolved.Symbol} (no such public symbol)");

        this.output.WriteLine(
            $"\n{report.Resolved.Count} resolved, {report.Ambiguous.Count} ambiguous, {report.Unresolved.Count} unresolved.");
        return report.Ambiguous.Count == 0 && report.Unresolved.Count == 0 ? Success : Failure;
    }

    /// <summary>Writes the extracted public-symbol surface as JSON, for debugging and tooling.</summary>
    public int Index()
    {
        var symbols = SymbolWalker.Extract(this.workspace.ReadSources())
            .Select(symbol => new
            {
                symbol.QualifiedName,
                symbol.Kind,
                symbol.FilePath,
                symbol.Line,
                symbol.IsPublic,
                Keys = symbol.Annotations.Select(annotation => annotation.Key).ToList(),
            });
        this.output.WriteLine(JsonSerializer.Serialize(symbols, IndexJsonOptions));
        return Success;
    }

    /// <summary>Checks the committed file is semantically equal to what the annotations would generate.</summary>
    public int Check()
    {
        var (result, _, errors) = this.Analyze();
        if (errors.Count > 0)
        {
            this.ReportErrors("Capability annotation check failed", errors);
            return Failure;
        }

        var expected = ComplianceYaml.Parse(this.workspace.ReadComplianceText());
        var actual = ComplianceYaml.FromEntries(result.Entries);
        var differences = ComplianceComparer.Diff(expected, actual);
        if (differences.Count > 0)
        {
            this.output.WriteLine("❌ sdk-compliance.yaml is out of sync with the capability annotations:");
            foreach (var difference in differences)
                this.output.WriteLine($"  - {difference}");
            return Failure;
        }

        this.output.WriteLine("✅ sdk-compliance.yaml matches the capability annotations.");
        return Success;
    }

    private (ComplianceResult Result, SpecModel Spec, IReadOnlyList<ValidationError> Errors) Analyze()
    {
        var symbols = SymbolWalker.Extract(this.workspace.ReadSources());
        var result = ComplianceModel.Build(symbols);
        var spec = SpecModel.Parse(this.workspace.ReadSpecDocuments());
        var errors = result.Errors
            .Concat(KeyValidator.ValidateKeys(result.Entries, spec.KnownIds))
            .ToList();
        return (result, spec, errors);
    }

    private void ReportErrors(string headline, IReadOnlyList<ValidationError> errors)
    {
        this.output.WriteLine($"❌ {headline}:");
        foreach (var error in errors)
            this.output.WriteLine($"  - {error.Message}");
    }
}
