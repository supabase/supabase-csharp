using Supabase.CapabilityMatrix;

return CliEntryPoint.Run(args, Console.Out, Console.Error);

/// <summary>
/// Parses command-line arguments, builds a <see cref="FileWorkspace" />, and
/// dispatches to the <see cref="CommandRunner" />. The only argument-handling and
/// process-exit logic; the work itself lives in the runner and its pure collaborators.
/// </summary>
internal static class CliEntryPoint
{
    private const int UsageError = 2;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            error.WriteLine(Usage);
            return UsageError;
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));

        var root = Option(options, "root", Directory.GetCurrentDirectory());
        var compliancePath = Option(options, "compliance", Path.Combine(root, "sdk-compliance.yaml"));
        var outputPath = Option(options, "out", compliancePath);
        var specDirectory = options.GetValueOrDefault("spec");

        if (command is "generate" or "validate" or "check" && specDirectory is null)
        {
            error.WriteLine($"'{command}' requires --spec <capabilities-directory>.");
            return UsageError;
        }

        var workspace = new FileWorkspace(root, specDirectory ?? string.Empty, compliancePath, outputPath);
        var runner = new CommandRunner(workspace, output);

        try
        {
            return command switch
            {
                "generate" => runner.Generate(),
                "validate" => runner.Validate(),
                "locate" => runner.Locate(),
                "index" => runner.Index(),
                "check" => runner.Check(),
                _ => Unknown(command, error),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            error.WriteLine($"❌ {exception.Message}");
            return UsageError;
        }
    }

    private static int Unknown(string command, TextWriter error)
    {
        error.WriteLine($"Unknown command '{command}'.");
        error.WriteLine(Usage);
        return UsageError;
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> arguments)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        string? pending = null;
        foreach (var argument in arguments)
        {
            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                pending = argument[2..];
                options[pending] = string.Empty;
            }
            else if (pending is not null)
            {
                options[pending] = argument;
                pending = null;
            }
        }

        return options;
    }

    private static string Option(IReadOnlyDictionary<string, string> options, string name, string fallback) =>
        options.TryGetValue(name, out var value) && value.Length > 0 ? value : fallback;

    private const string Usage =
        """
        Usage: Supabase.CapabilityMatrix <command> [options]

        Commands:
          generate   Write sdk-compliance.yaml from the in-code capability annotations.
          validate   Check the annotations (known keys, no conflicts); no file written.
          locate     Resolve the committed file's symbols against the source.
          check      Fail if the committed file is out of sync with the annotations.

        Options:
          --root <dir>         Repository root (default: current directory).
          --spec <dir>         Canonical capabilities/ directory (required: generate, validate, check).
          --compliance <file>  Committed sdk-compliance.yaml (default: <root>/sdk-compliance.yaml).
          --out <file>         Output path for generate (default: --compliance).
        """;
}
