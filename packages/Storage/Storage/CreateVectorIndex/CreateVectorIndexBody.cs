using System;
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
        this.DistanceMetric = ToWire(index.DistanceMetric);
        this.DataType = ToWire(index.DataType);
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
    public string DistanceMetric { get; }

    [JsonPropertyName("dataType")]
    public string DataType { get; }

    [JsonPropertyName("metadataConfiguration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MetadataConfigurationBody? MetadataConfiguration { get; }

    private static string ToWire(VectorDistanceMetric metric) => metric switch
    {
        VectorDistanceMetric.Cosine => "cosine",
        VectorDistanceMetric.Euclidean => "euclidean",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric."),
    };

    private static string ToWire(VectorDataType dataType) => dataType switch
    {
        VectorDataType.Float32 => "float32",
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unknown data type."),
    };

    internal sealed class MetadataConfigurationBody
    {
        internal MetadataConfigurationBody(IReadOnlyList<string> nonFilterableMetadataKeys) =>
            this.NonFilterableMetadataKeys = nonFilterableMetadataKeys;

        [JsonPropertyName("nonFilterableMetadataKeys")]
        public IReadOnlyList<string> NonFilterableMetadataKeys { get; }
    }
}
