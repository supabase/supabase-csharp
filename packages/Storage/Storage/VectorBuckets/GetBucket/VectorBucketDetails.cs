using System;

namespace Supabase.Storage.VectorBuckets.GetBucket;

/// <summary>
/// A vector bucket as the service describes it: its name, creation time and encryption.
/// </summary>
public sealed record VectorBucketDetails
{
    /// <summary>The name of the vector bucket, unique within the project.</summary>
    public required string VectorBucketName { get; init; }

    /// <summary>When the bucket was created, to the second.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The server-side encryption of the bucket. Null when the service reports none.</summary>
    public VectorBucketEncryption? Encryption { get; init; }
}
