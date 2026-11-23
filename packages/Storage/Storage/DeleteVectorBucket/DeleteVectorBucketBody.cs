using System.Text.Json.Serialization;

namespace Supabase.Storage.DeleteVectorBucket;

internal sealed class DeleteVectorBucketBody
{
    internal DeleteVectorBucketBody(string vectorBucketName) => this.VectorBucketName = vectorBucketName;

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }
}
