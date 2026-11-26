using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Storage.VectorBuckets.Common;

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
        Data = this.Data?.ToVectorData(),
        Metadata = this.Metadata,
    };

    internal sealed class VectorDataBody
    {
        [JsonPropertyName("float32")]
        public IReadOnlyList<float>? Float32 { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? OtherTypes { get; init; }

        internal VectorData? ToVectorData() =>
            this.Float32 is not null ? this.OfFloat32() : this.OfUnknownType();

        private VectorData OfFloat32() => new() { DataType = VectorDataType.Float32, Float32 = this.Float32 };

        private VectorData? OfUnknownType() =>
            this.OtherTypes is null ? null : new VectorData { DataType = VectorDataType.FromWire(this.OtherTypes.Keys.Min()) };
    }
}
