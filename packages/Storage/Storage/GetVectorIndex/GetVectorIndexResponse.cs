using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.GetVectorIndex;

internal sealed class GetVectorIndexResponse
{
    [JsonRequired]
    [JsonPropertyName("index")]
    public IndexBody Index { get; init; } = null!;

    internal VectorIndexDetails ToDetails() => new()
    {
        IndexName = this.Index.IndexName,
        Dimension = this.Index.Dimension,
        DistanceMetric = VectorDistanceMetric.FromWire(this.Index.DistanceMetric),
        DataType = VectorDataType.FromWire(this.Index.DataType),
        NonFilterableMetadataKeys = this.Index.MetadataConfiguration?.NonFilterableMetadataKeys ?? Array.Empty<string>(),
        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(this.Index.CreationTime),
    };

    internal sealed class IndexBody
    {
        [JsonRequired]
        [JsonPropertyName("indexName")]
        public string IndexName { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("dimension")]
        public int Dimension { get; init; }

        [JsonRequired]
        [JsonPropertyName("distanceMetric")]
        public string DistanceMetric { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("dataType")]
        public string DataType { get; init; } = null!;

        [JsonPropertyName("metadataConfiguration")]
        public MetadataConfigurationBody? MetadataConfiguration { get; init; }

        [JsonRequired]
        [JsonPropertyName("creationTime")]
        public long CreationTime { get; init; }
    }

    internal sealed class MetadataConfigurationBody
    {
        [JsonPropertyName("nonFilterableMetadataKeys")]
        public IReadOnlyList<string>? NonFilterableMetadataKeys { get; init; }
    }
}
