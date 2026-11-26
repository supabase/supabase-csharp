using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Supabase.Storage.Common;

internal sealed class IcebergPartitionSpecBody
{
    [JsonPropertyName("spec-id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SpecId { get; init; }

    [JsonRequired]
    [JsonPropertyName("fields")]
    public IReadOnlyList<PartitionFieldBody> Fields { get; init; } = null!;

    internal static IcebergPartitionSpecBody From(IcebergPartitionSpec spec) => new()
    {
        SpecId = spec.SpecId,
        Fields = spec.Fields.Select(PartitionFieldBody.From).ToList(),
    };

    internal IcebergPartitionSpec ToSpec() => new()
    {
        SpecId = this.SpecId,
        Fields = this.Fields.Select(field => field.ToField()).ToList(),
    };

    internal sealed class PartitionFieldBody
    {
        [JsonRequired]
        [JsonPropertyName("source-id")]
        public int SourceId { get; init; }

        [JsonPropertyName("field-id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? FieldId { get; init; }

        [JsonRequired]
        [JsonPropertyName("name")]
        public string Name { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("transform")]
        public string Transform { get; init; } = null!;

        internal static PartitionFieldBody From(IcebergPartitionField field) => new()
        {
            SourceId = field.SourceId,
            FieldId = field.FieldId,
            Name = field.Name,
            Transform = field.Transform.ToString(),
        };

        internal IcebergPartitionField ToField() => new()
        {
            SourceId = this.SourceId,
            FieldId = this.FieldId,
            Name = this.Name,
            Transform = IcebergTransform.FromWire(this.Transform),
        };
    }
}
