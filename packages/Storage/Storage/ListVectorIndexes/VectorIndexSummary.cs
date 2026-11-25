using System;

namespace Supabase.Storage.ListVectorIndexes;

/// <summary>
/// A vector index as a listing describes it: its name and creation time. Its settings come from
/// <see cref="Interfaces.IVectorBucket.GetIndexAsync"/>.
/// </summary>
public sealed record VectorIndexSummary
{
    /// <summary>The name of the index, unique within its vector bucket.</summary>
    public required string IndexName { get; init; }

    /// <summary>When the index was created, to the second.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
