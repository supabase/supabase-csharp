using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.DeleteBucket;

internal sealed class DeleteVectorBucketBody
{
    internal DeleteVectorBucketBody(string vectorBucketName) => this.VectorBucketName = vectorBucketName;

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }
}
