using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage.CreateVectorIndex;

internal sealed class CreateVectorIndexBody
{
    internal CreateVectorIndexBody(string vectorBucketName, NewVectorIndex index)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = index.IndexName;
        this.Dimension = index.Dimension;
        this.DistanceMetric = index.DistanceMetric?.ToString();
        this.DataType = index.DataType?.ToString();
        this.MetadataConfiguration = index.NonFilterableMetadataKeys is null
            ? null
            : new MetadataConfigurationBody(index.NonFilterableMetadataKeys);
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }

    [JsonPropertyName("dimension")]
    public int Dimension { get; }

    [JsonPropertyName("distanceMetric")]
    public string? DistanceMetric { get; }

    [JsonPropertyName("dataType")]
    public string? DataType { get; }

    [JsonPropertyName("metadataConfiguration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MetadataConfigurationBody? MetadataConfiguration { get; }

    internal sealed class MetadataConfigurationBody
    {
        internal MetadataConfigurationBody(IReadOnlyList<string> nonFilterableMetadataKeys) =>
            this.NonFilterableMetadataKeys = nonFilterableMetadataKeys;

        [JsonPropertyName("nonFilterableMetadataKeys")]
        public IReadOnlyList<string> NonFilterableMetadataKeys { get; }
    }
}
