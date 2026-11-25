namespace Supabase.Storage.ListAnalyticsBuckets;

/// <summary>
/// A column analytics buckets can be sorted by.
/// </summary>
public enum AnalyticsBucketSortColumn
{
    /// <summary>The bucket name.</summary>
    Name,

    /// <summary>When the bucket was created.</summary>
    CreatedAt,

    /// <summary>When the bucket was last updated.</summary>
    UpdatedAt,
}
