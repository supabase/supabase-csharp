using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage.GetVectors;

internal sealed class GetVectorsBody
{
    internal GetVectorsBody(string vectorBucketName, string indexName, IReadOnlyCollection<string> keys, VectorContent include)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = indexName;
        this.Keys = keys;
        this.ReturnData = RequestedOrOmitted(include, VectorContent.Data);
        this.ReturnMetadata = RequestedOrOmitted(include, VectorContent.Metadata);
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }

    [JsonPropertyName("keys")]
    public IReadOnlyCollection<string> Keys { get; }

    [JsonPropertyName("returnData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReturnData { get; }

    [JsonPropertyName("returnMetadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReturnMetadata { get; }

    private static bool? RequestedOrOmitted(VectorContent include, VectorContent content) =>
        (include & content) == content ? true : null;
}
