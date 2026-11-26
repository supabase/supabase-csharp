using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.CreateBucket;

internal sealed class CreateVectorBucketBody
{
    internal CreateVectorBucketBody(string vectorBucketName) => this.VectorBucketName = vectorBucketName;

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }
}
