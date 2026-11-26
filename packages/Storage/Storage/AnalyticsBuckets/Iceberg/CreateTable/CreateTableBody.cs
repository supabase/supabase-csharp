using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.CreateTable;

internal sealed class CreateTableBody
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    [JsonPropertyName("schema")]
    public IcebergSchemaBody Schema { get; init; } = null!;

    [JsonPropertyName("partition-spec")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IcebergPartitionSpecBody? PartitionSpec { get; init; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    internal static CreateTableBody For(IcebergTable table, IcebergSchema schema, CreateTableOptions? options) => new()
    {
        Name = table.Name,
        Schema = IcebergSchemaBody.From(schema),
        PartitionSpec = options?.PartitionSpec is { } spec ? IcebergPartitionSpecBody.From(spec) : null,
        Properties = options?.Properties is { Count: > 0 } properties ? properties : null,
    };
}
