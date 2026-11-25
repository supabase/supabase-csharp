using System;

namespace Supabase.Storage.ListVectorBuckets;

/// <summary>
/// A vector bucket as a listing describes it: its name and creation time.
/// </summary>
public sealed record VectorBucketSummary
{
    /// <summary>The name of the vector bucket, unique within the project.</summary>
    public required string VectorBucketName { get; init; }

    /// <summary>When the bucket was created, to the second.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
