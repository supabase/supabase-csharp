using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Gotrue.Common;

namespace Supabase.Gotrue.CreateSsoProvider;

internal sealed record SamlAttributeMappingBody
{
    private SamlAttributeMappingBody(IReadOnlyDictionary<string, SamlAttribute> mapping) =>
        this.Keys = mapping.ToDictionary(entry => entry.Key, entry => SamlAttributeBody.From(entry.Value));

    [JsonPropertyName("keys")]
    public IReadOnlyDictionary<string, SamlAttributeBody> Keys { get; }

    internal static SamlAttributeMappingBody? From(IReadOnlyDictionary<string, SamlAttribute>? mapping) =>
        mapping is null ? null : new SamlAttributeMappingBody(mapping);
}
