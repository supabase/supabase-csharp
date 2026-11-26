using System.Collections.Generic;

namespace Supabase.Storage.FileBuckets.Common;

/// <summary>
/// The lifecycle policy of a bucket.
/// </summary>
public sealed record BucketLifecycle
{
    /// <summary>The rules of the policy.</summary>
    public required IReadOnlyList<LifecycleRule> Rules { get; init; }
}
