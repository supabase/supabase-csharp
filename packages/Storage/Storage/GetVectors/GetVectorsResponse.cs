using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.GetVectors;

internal sealed class GetVectorsResponse
{
    [JsonRequired]
    [JsonPropertyName("vectors")]
    public IReadOnlyList<StoredVectorBody> Vectors { get; init; } = null!;

    internal IReadOnlyList<StoredVector> ToStoredVectors() =>
        this.Vectors.Select(vector => vector.ToStoredVector()).ToList();
}
