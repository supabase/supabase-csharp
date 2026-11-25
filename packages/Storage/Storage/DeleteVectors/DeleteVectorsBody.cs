using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage.DeleteVectors;

internal sealed class DeleteVectorsBody
{
    internal DeleteVectorsBody(string vectorBucketName, string indexName, IReadOnlyCollection<string> keys)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = indexName;
        this.Keys = keys;
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }

    [JsonPropertyName("keys")]
    public IReadOnlyCollection<string> Keys { get; }
}
