using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Supabase.Storage.VectorBuckets.Common;

namespace Supabase.Storage.VectorBuckets.QueryVectors;

internal sealed class QueryVectorsResponse
{
    [JsonPropertyName("vectors")]
    public IReadOnlyList<MatchBody>? Vectors { get; init; }

    [JsonPropertyName("distanceMetric")]
    public string? DistanceMetric { get; init; }

    [JsonPropertyName("nextToken")]
    public string? NextToken { get; init; }

    internal VectorQueryResult ToResult() => new()
    {
        Matches = (this.Vectors ?? Array.Empty<MatchBody>()).Select(match => match.ToMatch()).ToList(),
        DistanceMetric = this.DistanceMetric is null ? null : VectorDistanceMetric.FromWire(this.DistanceMetric),
        NextToken = this.NextToken,
    };

    internal sealed class MatchBody
    {
        [JsonRequired]
        [JsonPropertyName("key")]
        public string Key { get; init; } = null!;

        [JsonPropertyName("distance")]
        public float? Distance { get; init; }

        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }

        internal VectorMatch ToMatch() => new()
        {
            Key = this.Key,
            Distance = this.Distance,
            Metadata = this.Metadata,
        };
    }
}
