using System.Collections.Generic;

namespace Supabase.Storage.CreateVectorIndex;

/// <summary>
/// The settings of a vector index to create.
/// </summary>
public sealed record NewVectorIndex
{
    /// <summary>The name of the index, unique within its vector bucket. The service validates it.</summary>
    public required string IndexName { get; init; }

    /// <summary>The number of components of each vector. The service validates the range.</summary>
    public required int Dimension { get; init; }

    /// <summary>The measure used to rank vectors by similarity.</summary>
    public required VectorDistanceMetric DistanceMetric { get; init; }

    /// <summary>The numeric type of the vector components. Defaults to <see cref="VectorDataType.Float32"/>.</summary>
    public VectorDataType DataType { get; init; } = VectorDataType.Float32;

    /// <summary>
    /// Metadata keys that can be stored with a vector but not used to filter queries.
    /// When null the index declares none; an empty list is sent as is and left to the service to judge.
    /// </summary>
    public IReadOnlyList<string>? NonFilterableMetadataKeys { get; init; }
}
