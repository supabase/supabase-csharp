using System;
using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.GetBucket;

internal sealed class GetVectorBucketResponse
{
    [JsonRequired]
    [JsonPropertyName("vectorBucket")]
    public BucketBody VectorBucket { get; init; } = null!;

    internal VectorBucketDetails ToDetails() => new()
    {
        VectorBucketName = this.VectorBucket.VectorBucketName,
        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(this.VectorBucket.CreationTime),
        Encryption = this.VectorBucket.EncryptionConfiguration?.ToEncryption(),
    };

    internal sealed class BucketBody
    {
        [JsonRequired]
        [JsonPropertyName("vectorBucketName")]
        public string VectorBucketName { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("creationTime")]
        public long CreationTime { get; init; }

        [JsonPropertyName("encryptionConfiguration")]
        public EncryptionBody? EncryptionConfiguration { get; init; }
    }

    internal sealed class EncryptionBody
    {
        [JsonPropertyName("sseType")]
        public string? SseType { get; init; }

        [JsonPropertyName("kmsKeyArn")]
        public string? KmsKeyArn { get; init; }

        internal VectorBucketEncryption ToEncryption() => new() { SseType = this.SseType, KmsKeyArn = this.KmsKeyArn };
    }
}
