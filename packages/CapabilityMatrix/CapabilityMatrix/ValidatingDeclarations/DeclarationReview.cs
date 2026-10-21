using Supabase.CapabilityMatrix.CanonicalMatrix;
using Supabase.CapabilityMatrix.Capabilities;

namespace Supabase.CapabilityMatrix.ValidatingDeclarations;

/// <summary>
/// Reviews the capabilities declared in code against the canonical catalogue, returning
/// one problem per authoring mistake: a feature key that does not exist, a feature
/// declared with two statuses, or one implemented only privately yet carrying no note.
/// </summary>
public static class DeclarationReview
{
    public static IReadOnlyList<DeclarationProblem> Of(
        IReadOnlyList<DeclaredCapability> declarations, FeatureCatalogue catalogue)
    {
        var features = declarations
            .GroupBy(declaration => declaration.FeatureKey, StringComparer.Ordinal)
            .OrderBy(feature => feature.Key, StringComparer.Ordinal)
            .ToList();

        return
        [
            .. features.Where(feature => !catalogue.Knows(feature.Key)).Select(DeclarationProblem.UnknownFeature),
            .. features.Where(DeclaredWithTwoStatuses).Select(DeclarationProblem.ConflictingStatus),
            .. features.Where(ImplementedOnlyPrivatelyWithoutANote).Select(DeclarationProblem.MissingNote),
        ];
    }

    private static bool DeclaredWithTwoStatuses(IGrouping<string, DeclaredCapability> feature) =>
        feature.Select(declaration => declaration.Status).Distinct().Count() > 1;

    private static bool ImplementedOnlyPrivatelyWithoutANote(IGrouping<string, DeclaredCapability> feature) =>
        feature.All(declaration => !declaration.FromPublicApi)
            && feature.All(declaration => string.IsNullOrEmpty(declaration.Note));
}
