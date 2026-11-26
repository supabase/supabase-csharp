using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.QueryVectors;

internal sealed class QueryVectorsBody
{
    internal QueryVectorsBody(string vectorBucketName, string indexName, IReadOnlyList<float> queryVector, int topK, QueryVectorsOptions options)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = indexName;
        this.QueryVector = new QueryVectorBody(queryVector);
        this.TopK = topK;
        this.Filter = options.Filter;
        this.NextToken = options.NextToken;
        this.ReturnDistance = RequestedOrOmitted(options.Include, VectorMatchContent.Distance);
        this.ReturnMetadata = RequestedOrOmitted(options.Include, VectorMatchContent.Metadata);
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }

    [JsonPropertyName("queryVector")]
    public QueryVectorBody QueryVector { get; }

    [JsonPropertyName("topK")]
    public int TopK { get; }

    [JsonPropertyName("filter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Filter { get; }

    [JsonPropertyName("nextToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextToken { get; }

    [JsonPropertyName("returnDistance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReturnDistance { get; }

    [JsonPropertyName("returnMetadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReturnMetadata { get; }

    private static bool? RequestedOrOmitted(VectorMatchContent include, VectorMatchContent content) =>
        (include & content) == content ? true : null;

    internal sealed class QueryVectorBody
    {
        internal QueryVectorBody(IReadOnlyList<float> float32) => this.Float32 = float32;

        [JsonPropertyName("float32")]
        public IReadOnlyList<float> Float32 { get; }
    }
}
