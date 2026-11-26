using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.VectorBuckets.ListIndexes;

internal sealed class ListVectorIndexesResponse
{
    [JsonPropertyName("indexes")]
    public IReadOnlyList<IndexBody>? Indexes { get; init; }

    [JsonPropertyName("nextToken")]
    public string? NextToken { get; init; }

    internal Page<VectorIndexSummary> ToPage() => new()
    {
        Items = (this.Indexes ?? Array.Empty<IndexBody>()).Select(index => index.ToSummary()).ToList(),
        NextPageToken = this.NextToken,
    };

    internal sealed class IndexBody
    {
        [JsonRequired]
        [JsonPropertyName("indexName")]
        public string IndexName { get; init; } = null!;

        [JsonRequired]
        [JsonPropertyName("creationTime")]
        public long CreationTime { get; init; }

        internal VectorIndexSummary ToSummary() => new()
        {
            IndexName = this.IndexName,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(this.CreationTime),
        };
    }
}
