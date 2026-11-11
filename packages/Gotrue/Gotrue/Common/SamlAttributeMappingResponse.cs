using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Common;

internal sealed record SamlAttributeMappingResponse
{
    [JsonPropertyName("keys")]
    public IReadOnlyDictionary<string, SamlAttributeResponse>? Keys { get; init; }

    internal static IReadOnlyDictionary<string, SamlAttribute> Read(SamlAttributeMappingResponse? mapping) =>
        (mapping?.Keys ?? new Dictionary<string, SamlAttributeResponse>())
            .ToDictionary(entry => entry.Key, entry => entry.Value.ToSamlAttribute());
}
