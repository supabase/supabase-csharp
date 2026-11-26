using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.VectorBuckets.Common;

namespace Supabase.Storage.VectorBuckets.GetVectors;

internal sealed class GetVectorsResponse
{
    [JsonPropertyName("vectors")]
    public IReadOnlyList<StoredVectorBody>? Vectors { get; init; }

    internal IReadOnlyList<StoredVector> ToStoredVectors() =>
        (this.Vectors ?? Array.Empty<StoredVectorBody>()).Select(vector => vector.ToStoredVector()).ToList();
}
