using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Supabase.CapabilityMatrix;

/// <summary>A capability group within an area, carrying the ordered feature ids assigned to it.</summary>
public sealed record GroupSpec(string Id, string Title, IReadOnlyList<string> FeatureIds);

/// <summary>A product area (auth, database, ...) with its ordered groups and feature ids.</summary>
public sealed record AreaSpec(
    string Id,
    string Title,
    IReadOnlyList<GroupSpec> Groups,
    IReadOnlyList<string> UngroupedFeatureIds);

/// <summary>
/// The canonical capability spec loaded from the matrix repo's
/// <c>capabilities/*.yaml</c> files. Provides the set of known feature ids (for
/// validation) and the area → group → feature ordering (for emitting a stable file).
/// </summary>
public sealed class SpecModel
{
    public IReadOnlyList<AreaSpec> Areas { get; }
    public IReadOnlySet<string> KnownIds { get; }

    private SpecModel(IReadOnlyList<AreaSpec> areas, IReadOnlySet<string> knownIds)
    {
        this.Areas = areas;
        this.KnownIds = knownIds;
    }

    /// <summary>Parses the given capability-area YAML documents into a spec model.</summary>
    public static SpecModel Parse(IEnumerable<string> areaDocuments)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var areas = new List<AreaSpec>();
        var knownIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var document in areaDocuments)
        {
            var dto = deserializer.Deserialize<AreaFileDto>(document);
            if (dto?.Area is null)
                continue;

            var features = dto.Features ?? [];
            foreach (var feature in features.Where(f => f.Id is not null))
                knownIds.Add(feature.Id!);

            var groups = (dto.Groups ?? [])
                .Where(group => group.Id is not null)
                .Select(group => new GroupSpec(
                    group.Id!,
                    group.Title ?? group.Id!,
                    features.Where(f => f.Id is not null && f.Group == group.Id).Select(f => f.Id!).ToList()))
                .ToList();

            var ungrouped = features
                .Where(f => f.Id is not null && string.IsNullOrEmpty(f.Group))
                .Select(f => f.Id!)
                .ToList();

            areas.Add(new AreaSpec(dto.Area, dto.Title ?? dto.Area, groups, ungrouped));
        }

        areas = areas.OrderBy(area => area.Id, StringComparer.Ordinal).ToList();
        return new SpecModel(areas, knownIds);
    }

    private sealed class AreaFileDto
    {
        public string? Area { get; set; }
        public string? Title { get; set; }
        public List<GroupDto>? Groups { get; set; }
        public List<FeatureDto>? Features { get; set; }
    }

    private sealed class GroupDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
    }

    private sealed class FeatureDto
    {
        public string? Id { get; set; }
        public string? Group { get; set; }
    }
}
