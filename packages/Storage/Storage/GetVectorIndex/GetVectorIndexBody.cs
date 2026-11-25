using System.Text.Json.Serialization;

namespace Supabase.Storage.GetVectorIndex;

internal sealed class GetVectorIndexBody
{
    internal GetVectorIndexBody(string vectorBucketName, string indexName)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = indexName;
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }
}
