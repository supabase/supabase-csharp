using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.VectorBuckets.Common;

namespace Supabase.Storage.VectorBuckets.ListVectors;

internal sealed class ListVectorsResponse
{
    [JsonPropertyName("vectors")]
    public IReadOnlyList<StoredVectorBody>? Vectors { get; init; }

    [JsonPropertyName("nextToken")]
    public string? NextToken { get; init; }

    internal VectorListPage<StoredVector> ToPage() => new()
    {
        Items = (this.Vectors ?? Array.Empty<StoredVectorBody>()).Select(vector => vector.ToStoredVector()).ToList(),
        NextToken = this.NextToken,
    };
}
