using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Supabase.Storage.Common;

internal sealed class IcebergFieldBody
{
    [JsonRequired]
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonRequired]
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    [JsonRequired]
    [JsonPropertyName("type")]
    [JsonConverter(typeof(IcebergTypeConverter))]
    public IcebergType Type { get; init; } = null!;

    [JsonRequired]
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("doc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Doc { get; init; }

    internal static IReadOnlyList<IcebergFieldBody> FromAll(IEnumerable<IcebergField> fields) =>
        fields.Select(From).ToList();

    internal static IReadOnlyList<IcebergField> ToFields(IEnumerable<IcebergFieldBody> bodies) =>
        bodies.Select(body => body.ToField()).ToList();

    private static IcebergFieldBody From(IcebergField field) => new()
    {
        Id = field.Id,
        Name = field.Name,
        Type = field.Type,
        Required = field.Required,
        Doc = field.Doc,
    };

    private IcebergField ToField() => new()
    {
        Id = this.Id,
        Name = this.Name,
        Type = this.Type,
        Required = this.Required,
        Doc = this.Doc,
    };
}
