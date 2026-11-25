using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.ListVectors;

internal sealed class ListVectorsBody
{
    internal ListVectorsBody(string vectorBucketName, string indexName, ListVectorsOptions options)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = indexName;
        this.MaxResults = options.MaxResults;
        this.NextToken = options.NextToken;
        this.ReturnData = VectorContentRequest.RequestedOrOmitted(options.Include, VectorContent.Data);
        this.ReturnMetadata = VectorContentRequest.RequestedOrOmitted(options.Include, VectorContent.Metadata);
        this.SegmentCount = options.Segment?.Count;
        this.SegmentIndex = options.Segment?.Index;
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }

    [JsonPropertyName("maxResults")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxResults { get; }

    [JsonPropertyName("nextToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextToken { get; }

    [JsonPropertyName("returnData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReturnData { get; }

    [JsonPropertyName("returnMetadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReturnMetadata { get; }

    [JsonPropertyName("segmentCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SegmentCount { get; }

    [JsonPropertyName("segmentIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SegmentIndex { get; }
}
