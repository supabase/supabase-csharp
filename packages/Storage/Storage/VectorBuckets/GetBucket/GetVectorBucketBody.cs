using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.GetBucket;

internal sealed class GetVectorBucketBody
{
    internal GetVectorBucketBody(string vectorBucketName) => this.VectorBucketName = vectorBucketName;

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }
}
