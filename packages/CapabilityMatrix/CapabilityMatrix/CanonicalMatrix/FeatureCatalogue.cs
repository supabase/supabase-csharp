using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Supabase.CapabilityMatrix.CanonicalMatrix;

/// <summary>A capability group within an area, holding its feature keys in canonical order.</summary>
public sealed record CatalogueGroup(string Id, string Title, IReadOnlyList<string> FeatureKeys);

/// <summary>A product area (auth, database, …) with its ordered groups and feature keys.</summary>
public sealed record CatalogueArea(
    string Id, string Title, IReadOnlyList<CatalogueGroup> Groups, IReadOnlyList<string> UngroupedFeatureKeys);

/// <summary>
/// The canonical feature catalogue, loaded from the matrix repo's <c>capabilities/*.yaml</c>.
/// Answers whether a feature key is real (for validation) and in what order areas, groups
/// and features appear (for rendering a stable compliance file).
/// </summary>
public sealed class FeatureCatalogue
{
    private readonly HashSet<string> knownKeys;

    public IReadOnlyList<CatalogueArea> Areas { get; }

    private FeatureCatalogue(IReadOnlyList<CatalogueArea> areas)
    {
        this.Areas = areas;
        this.knownKeys = [.. areas.SelectMany(AllKeys)];
    }

    public bool Knows(string featureKey) => this.knownKeys.Contains(featureKey);

    /// <summary>How many features the matrix defines in total, across every area.</summary>
    public int FeatureCount => this.knownKeys.Count;

    public static FeatureCatalogue From(IEnumerable<string> areaDocuments)
    {
        var yaml = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var areas = areaDocuments
            .Select(document => yaml.Deserialize<AreaDocument>(document))
            .Where(area => area?.Area is not null)
            .Select(ToArea)
            .OrderBy(area => area.Id, StringComparer.Ordinal)
            .ToList();

        return new FeatureCatalogue(areas);
    }

    private static CatalogueArea ToArea(AreaDocument document)
    {
        var features = (document.Features ?? []).Where(feature => feature.Id is not null).ToList();
        var groups = (document.Groups ?? [])
            .Where(group => group.Id is not null)
            .Select(group => new CatalogueGroup(
                group.Id!, group.Title ?? group.Id!,
                [.. features.Where(feature => feature.Group == group.Id).Select(feature => feature.Id!)]))
            .ToList();
        var ungrouped = features.Where(feature => string.IsNullOrEmpty(feature.Group)).Select(feature => feature.Id!).ToList();

        return new CatalogueArea(document.Area!, document.Title ?? document.Area!, groups, ungrouped);
    }

    private static IEnumerable<string> AllKeys(CatalogueArea area) =>
        area.Groups.SelectMany(group => group.FeatureKeys).Concat(area.UngroupedFeatureKeys);

    private sealed class AreaDocument
    {
        public string? Area { get; set; }
        public string? Title { get; set; }
        public List<GroupEntry>? Groups { get; set; }
        public List<FeatureEntry>? Features { get; set; }
    }

    private sealed class GroupEntry
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
    }

    private sealed class FeatureEntry
    {
        public string? Id { get; set; }
        public string? Group { get; set; }
    }
}
