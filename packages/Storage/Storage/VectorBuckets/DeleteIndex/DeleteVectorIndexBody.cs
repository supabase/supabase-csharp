using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.DeleteIndex;

internal sealed class DeleteVectorIndexBody
{
    internal DeleteVectorIndexBody(string vectorBucketName, string indexName)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = indexName;
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }
}
