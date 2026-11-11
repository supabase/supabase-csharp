using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Gotrue.Common;

internal sealed record SamlAttributeBody
{
    private SamlAttributeBody(SamlAttribute attribute)
    {
        this.Names = attribute.Names;
        this.Default = attribute.Default;
        this.Array = attribute.KeepsAllValues;
    }

    [JsonPropertyName("names")]
    public IReadOnlyList<string> Names { get; }

    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Default { get; }

    [JsonPropertyName("array")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Array { get; }

    internal static SamlAttributeBody From(SamlAttribute attribute) => new(attribute);
}
