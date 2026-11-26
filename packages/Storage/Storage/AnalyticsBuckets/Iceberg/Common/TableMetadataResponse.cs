using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

internal sealed class TableMetadataResponse
{
    [JsonPropertyName("metadata-location")]
    public string? MetadataLocation { get; init; }

    [JsonRequired]
    [JsonPropertyName("metadata")]
    public MetadataBody Metadata { get; init; } = null!;

    internal IcebergTableMetadata ToMetadata() => new()
    {
        MetadataLocation = this.MetadataLocation,
        TableUuid = this.Metadata.TableUuid,
        FormatVersion = this.Metadata.FormatVersion,
        Location = this.Metadata.Location,
        LastUpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(this.Metadata.LastUpdatedMs),
        CurrentSchema = this.Metadata.CurrentSchema(),
        DefaultPartitionSpec = this.Metadata.DefaultPartitionSpec(),
        LastColumnId = this.Metadata.LastColumnId,
        LastPartitionId = this.Metadata.LastPartitionId,
        Properties = this.Metadata.Properties ?? new Dictionary<string, string>(),
    };

    internal sealed class MetadataBody
    {
        [JsonRequired]
        [JsonPropertyName("format-version")]
        public int FormatVersion { get; init; }

        [JsonRequired]
        [JsonPropertyName("table-uuid")]
        public Guid TableUuid { get; init; }

        [JsonRequired]
        [JsonPropertyName("location")]
        public string Location { get; init; } = null!;

        [JsonPropertyName("last-updated-ms")]
        public long LastUpdatedMs { get; init; }

        [JsonPropertyName("last-column-id")]
        public int LastColumnId { get; init; }

        [JsonPropertyName("schemas")]
        public IReadOnlyList<IcebergSchemaBody>? Schemas { get; init; }

        [JsonPropertyName("current-schema-id")]
        public int CurrentSchemaId { get; init; }

        [JsonPropertyName("partition-specs")]
        public IReadOnlyList<IcebergPartitionSpecBody>? PartitionSpecs { get; init; }

        [JsonPropertyName("default-spec-id")]
        public int DefaultSpecId { get; init; }

        [JsonPropertyName("last-partition-id")]
        public int LastPartitionId { get; init; }

        [JsonPropertyName("properties")]
        public Dictionary<string, string>? Properties { get; init; }

        internal IcebergSchema CurrentSchema() =>
            this.Schemas?.FirstOrDefault(schema => schema.SchemaId == this.CurrentSchemaId)?.ToSchema()
            ?? new IcebergSchema { Fields = Array.Empty<IcebergField>(), SchemaId = this.CurrentSchemaId };

        internal IcebergPartitionSpec DefaultPartitionSpec() =>
            this.PartitionSpecs?.FirstOrDefault(spec => spec.SpecId == this.DefaultSpecId)?.ToSpec()
            ?? new IcebergPartitionSpec { Fields = Array.Empty<IcebergPartitionField>(), SpecId = this.DefaultSpecId };
    }
}
