using Supabase.CapabilityMatrix.CanonicalMatrix;
using Supabase.CapabilityMatrix.Capabilities;
using Supabase.CapabilityMatrix.DeclaredInSource;
using Supabase.CapabilityMatrix.GeneratingTheComplianceFile;
using Supabase.CapabilityMatrix.ValidatingDeclarations;

namespace Supabase.CapabilityMatrix;

public enum ExitCode
{
    Success = 0,
    ProblemsFound = 1,
    UsageError = 2,
}

/// <summary>
/// The two use cases behind the command line — <c>generate</c> the compliance file from
/// the declared capabilities, and <c>validate</c> those declarations against the catalogue.
/// Reads and writes the filesystem here; the work itself is in the pure collaborators.
/// </summary>
public static class CommandLine
{
    public static ExitCode Run(string[] arguments, TextWriter output, TextWriter error)
    {
        if (arguments is not [var command, .. var rest])
        {
            error.WriteLine(Usage);
            return ExitCode.UsageError;
        }

        var options = Options.From(rest);
        try
        {
            return command switch
            {
                "generate" => Generate(options, output, error),
                "validate" => Validate(options, output, error),
                _ => Unknown(command, error),
            };
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            error.WriteLine(failure.Message);
            return ExitCode.UsageError;
        }
    }

    private static ExitCode Generate(Options options, TextWriter output, TextWriter error)
    {
        if (options.Spec is null)
            return MissingSpec("generate", error);

        var declarations = DeclarationsUnder(options.Root);
        var catalogue = CatalogueFrom(options.Spec);

        var problems = DeclarationReview.Of(declarations, catalogue);
        if (problems.Count > 0)
            return Report(problems, error);

        var features = FeatureCompliance.Consolidate(declarations);
        File.WriteAllText(options.Output, ComplianceFile.For(features, catalogue));
        output.WriteLine($"Wrote {options.Output} ({features.Count} features).");
        return ExitCode.Success;
    }

    private static ExitCode Validate(Options options, TextWriter output, TextWriter error)
    {
        if (options.Spec is null)
            return MissingSpec("validate", error);

        var declarations = DeclarationsUnder(options.Root);
        var catalogue = CatalogueFrom(options.Spec);

        var problems = DeclarationReview.Of(declarations, catalogue);
        if (problems.Count > 0)
            return Report(problems, error);

        output.WriteLine(Summary(declarations, catalogue));
        output.WriteLine(
            "Every feature id is known to the matrix, no feature mixes statuses, " +
            "and every non-public declaration carries a note.");
        return ExitCode.Success;
    }

    private static string Summary(IReadOnlyList<DeclaredCapability> declarations, FeatureCatalogue catalogue)
    {
        var features = declarations.Select(declaration => declaration.FeatureKey).Distinct(StringComparer.Ordinal).Count();
        var symbols = declarations.Select(declaration => declaration.Symbol).Distinct(StringComparer.Ordinal).Count();
        return $"Validated {Count(declarations.Count, "capability annotation")} on {Count(symbols, "symbol")}, " +
               $"covering {features} of {catalogue.FeatureCount} matrix features.";
    }

    private static ExitCode Report(IReadOnlyList<DeclarationProblem> problems, TextWriter error)
    {
        if (problems.Count > 0)
            error.WriteLine($"{Count(problems.Count, "problem")} in the capability annotations:");
        foreach (var problem in problems)
            error.WriteLine($"  - {problem.Message}");
        return problems.Count == 0 ? ExitCode.Success : ExitCode.ProblemsFound;
    }

    private static string Count(int number, string noun) =>
        $"{number} {noun}{(number == 1 ? "" : "s")}";

    private static IReadOnlyList<DeclaredCapability> DeclarationsUnder(string root)
    {
        var packages = Path.Combine(root, "packages");
        var sources = Directory.Exists(packages)
            ? Directory.EnumerateFiles(packages, "*.cs", SearchOption.AllDirectories)
                .Select(path => (Relative: Path.GetRelativePath(root, path).Replace('\\', '/'), Full: path))
                .Where(file => SdkSourceFiles.Includes(file.Relative))
                .Select(file => new SourceFile(file.Relative, File.ReadAllText(file.Full)))
            : [];
        return CapabilityDeclarations.From(sources);
    }

    private static FeatureCatalogue CatalogueFrom(string specDirectory) =>
        Directory.Exists(specDirectory)
            ? FeatureCatalogue.From(Directory.EnumerateFiles(specDirectory, "*.yaml").OrderBy(path => path, StringComparer.Ordinal).Select(File.ReadAllText))
            : throw new DirectoryNotFoundException($"Capability spec directory not found: {specDirectory}");

    private static ExitCode MissingSpec(string command, TextWriter error)
    {
        error.WriteLine($"'{command}' needs --spec <capabilities-directory>.");
        return ExitCode.UsageError;
    }

    private static ExitCode Unknown(string command, TextWriter error)
    {
        error.WriteLine($"Unknown command '{command}'.");
        error.WriteLine(Usage);
        return ExitCode.UsageError;
    }

    private const string Usage =
        """
        Usage: Supabase.CapabilityMatrix <generate|validate> --spec <dir>

          generate   Write ./sdk-compliance.yaml from the capabilities declared in the SDK source.
          validate   Report declarations that name an unknown feature, conflict, or lack a required note.

        Run from the repository root: the SDK source is read from ./packages and
        generate writes ./sdk-compliance.yaml (the only location the matrix accepts).

          --spec   Canonical capabilities/ directory (required).
        """;

    private sealed record Options(string Root, string? Spec)
    {
        public string Output => Path.Combine(this.Root, "sdk-compliance.yaml");

        public static Options From(IReadOnlyList<string> arguments)
        {
            var flags = Enumerable.Range(0, arguments.Count)
                .Where(index => arguments[index].StartsWith("--", StringComparison.Ordinal) && index + 1 < arguments.Count)
                .ToDictionary(index => arguments[index][2..], index => arguments[index + 1]);

            return new Options(Directory.GetCurrentDirectory(), flags.GetValueOrDefault("spec"));
        }
    }
}
