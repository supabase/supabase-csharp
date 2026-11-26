using System;
using System.Collections.Generic;
using Supabase.Storage.VectorBuckets.Common;

namespace Supabase.Storage.VectorBuckets.QueryVectors;

/// <summary>
/// The outcome of a similarity search.
/// </summary>
public sealed record VectorQueryResult
{
    /// <summary>The closest vectors, nearest first.</summary>
    public IReadOnlyList<VectorMatch> Matches { get; init; } = Array.Empty<VectorMatch>();

    /// <summary>The metric the index ranked the matches by, or null when the service did not report it.</summary>
    public VectorDistanceMetric? DistanceMetric { get; init; }

    /// <summary>The token that requests the next matches, or null when there are no more.</summary>
    public string? NextToken { get; init; }
}
