using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Storage.Common;

internal sealed class StoredVectorBody
{
    [JsonRequired]
    [JsonPropertyName("key")]
    public string Key { get; init; } = null!;

    [JsonPropertyName("data")]
    public VectorDataBody? Data { get; init; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }

    internal StoredVector ToStoredVector() => new()
    {
        Key = this.Key,
        Data = this.Data?.Float32,
        Metadata = this.Metadata,
    };

    internal sealed class VectorDataBody
    {
        [JsonPropertyName("float32")]
        public IReadOnlyList<float>? Float32 { get; init; }
    }
}
