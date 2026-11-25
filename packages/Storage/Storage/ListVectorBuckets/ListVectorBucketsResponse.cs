using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.ListVectorBuckets;

internal sealed class ListVectorBucketsResponse
{
    [JsonPropertyName("vectorBuckets")]
    public IReadOnlyList<BucketBody>? VectorBuckets { get; init; }

    [JsonPropertyName("nextToken")]
    public string? NextToken { get; init; }

    internal VectorListPage<VectorBucketSummary> ToPage() => new()
    {
        Items = (this.VectorBuckets ?? Array.Empty<BucketBody>()).Select(bucket => bucket.ToSummary()).ToList(),
        NextToken = this.NextToken,
    };

    internal sealed class BucketBody
    {
        [JsonRequired]
        [JsonPropertyName("vectorBucketName")]
        public string VectorBucketName { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("creationTime")]
        public long CreationTime { get; init; }

        internal VectorBucketSummary ToSummary() => new()
        {
            VectorBucketName = this.VectorBucketName,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(this.CreationTime),
        };
    }
}
