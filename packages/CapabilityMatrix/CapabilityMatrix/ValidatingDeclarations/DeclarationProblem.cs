using Supabase.CapabilityMatrix.Capabilities;

namespace Supabase.CapabilityMatrix.ValidatingDeclarations;

/// <summary>Something wrong with how a feature is declared in code, phrased for the author to fix.</summary>
public sealed record DeclarationProblem(string Message)
{
    public static DeclarationProblem UnknownFeature(IGrouping<string, DeclaredCapability> feature) =>
        new($"'{feature.Key}' is not a capability-matrix feature id (declared by {Symbols(feature)}). Check for a typo.");

    public static DeclarationProblem ConflictingStatus(IGrouping<string, DeclaredCapability> feature) =>
        new($"'{feature.Key}' is declared both implemented and partial across members — a feature has one status.");

    public static DeclarationProblem MissingNote(IGrouping<string, DeclaredCapability> feature) =>
        new($"'{feature.Key}' is implemented only through non-public API and needs a note explaining how it is exposed.");

    private static string Symbols(IGrouping<string, DeclaredCapability> feature) =>
        string.Join(", ", feature.Select(declaration => declaration.Symbol).Distinct(StringComparer.Ordinal));
}
