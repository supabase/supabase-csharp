namespace Supabase.Storage.ListAnalyticsBuckets;

/// <summary>
/// Pagination, sort and search options for listing analytics buckets. An option left unset is not sent, and the
/// service applies its default: every bucket, sorted by name in ascending order.
/// </summary>
public sealed record ListAnalyticsBucketsOptions
{
    /// <summary>The maximum number of buckets to return. The service requires at least 1.</summary>
    public int? Limit { get; init; }

    /// <summary>The number of buckets to skip. The service requires at least 0.</summary>
    public int? Offset { get; init; }

    /// <summary>The order the buckets are listed in.</summary>
    public AnalyticsBucketSort? Sort { get; init; }

    /// <summary>Returns only the buckets whose name contains this text, compared case-sensitively.</summary>
    public string? Search { get; init; }
}
