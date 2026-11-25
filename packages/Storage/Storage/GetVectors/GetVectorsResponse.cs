using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.GetVectors;

internal sealed class GetVectorsResponse
{
    [JsonPropertyName("vectors")]
    public IReadOnlyList<StoredVectorBody>? Vectors { get; init; }

    internal IReadOnlyList<StoredVector> ToStoredVectors() =>
        (this.Vectors ?? Array.Empty<StoredVectorBody>()).Select(vector => vector.ToStoredVector()).ToList();
}
