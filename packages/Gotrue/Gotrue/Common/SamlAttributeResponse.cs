using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Common;

internal sealed record SamlAttributeResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("names")]
    public IReadOnlyList<string>? Names { get; init; }

    [JsonPropertyName("default")]
    public object? Default { get; init; }

    [JsonPropertyName("array")]
    public bool Array { get; init; }

    internal SamlAttribute ToSamlAttribute() =>
        new()
        {
            Names = this.NamesInLookupOrder(),
            Default = this.Default,
            KeepsAllValues = this.Array,
        };

    private IReadOnlyList<string> NamesInLookupOrder() =>
        this.SingleName().Concat(this.Names ?? System.Array.Empty<string>()).ToList();

    private IEnumerable<string> SingleName() =>
        this.Name is { } name ? new[] { name } : Enumerable.Empty<string>();
}
