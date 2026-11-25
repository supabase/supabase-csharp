using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Supabase.Storage.PutVectors;

internal sealed class PutVectorsBody
{
    internal PutVectorsBody(string vectorBucketName, string indexName, IReadOnlyCollection<NewVector> vectors)
    {
        this.VectorBucketName = vectorBucketName;
        this.IndexName = indexName;
        this.Vectors = vectors.Select(vector => new VectorBody(vector)).ToList();
    }

    [JsonPropertyName("vectorBucketName")]
    public string VectorBucketName { get; }

    [JsonPropertyName("indexName")]
    public string IndexName { get; }

    [JsonPropertyName("vectors")]
    public IReadOnlyList<VectorBody> Vectors { get; }

    internal sealed class VectorBody
    {
        internal VectorBody(NewVector vector)
        {
            this.Key = vector.Key;
            this.Data = new DataBody(vector.Data);
            this.Metadata = vector.Metadata;
        }

        [JsonPropertyName("key")]
        public string Key { get; }

        [JsonPropertyName("data")]
        public DataBody Data { get; }

        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyDictionary<string, JsonElement>? Metadata { get; }
    }

    internal sealed class DataBody
    {
        internal DataBody(IReadOnlyList<float> float32) => this.Float32 = float32;

        [JsonPropertyName("float32")]
        public IReadOnlyList<float> Float32 { get; }
    }
}
