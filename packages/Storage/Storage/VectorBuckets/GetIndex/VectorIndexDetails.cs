using System;
using System.Collections.Generic;
using Supabase.Storage.VectorBuckets.Common;

namespace Supabase.Storage.VectorBuckets.GetIndex;

/// <summary>
/// The settings and creation time of a vector index, as the service describes it.
/// </summary>
public sealed record VectorIndexDetails
{
    /// <summary>The name of the index, unique within its vector bucket.</summary>
    public required string IndexName { get; init; }

    /// <summary>The number of components of each vector.</summary>
    public required int Dimension { get; init; }

    /// <summary>The measure used to rank vectors by similarity.</summary>
    public required VectorDistanceMetric DistanceMetric { get; init; }

    /// <summary>The numeric type of the vector components.</summary>
    public required VectorDataType DataType { get; init; }

    /// <summary>Metadata keys that can be stored with a vector but not used to filter queries. Empty when the index declares none.</summary>
    public IReadOnlyList<string> NonFilterableMetadataKeys { get; init; } = Array.Empty<string>();

    /// <summary>When the index was created, to the second.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
