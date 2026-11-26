using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

internal sealed class IcebergSchemaBody
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "struct";

    [JsonRequired]
    [JsonPropertyName("fields")]
    public IReadOnlyList<IcebergFieldBody> Fields { get; init; } = null!;

    [JsonPropertyName("schema-id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SchemaId { get; init; }

    internal static IcebergSchemaBody From(IcebergSchema schema) => new()
    {
        Fields = IcebergFieldBody.FromAll(schema.Fields),
    };

    internal IcebergSchema ToSchema() => new()
    {
        Fields = IcebergFieldBody.ToFields(this.Fields),
        SchemaId = this.SchemaId,
    };
}
