using Supabase.Storage.VectorBuckets.Common;

namespace Supabase.Storage.VectorBuckets.ListIndexes;

/// <summary>
/// Filter and paging options for listing the vector indexes of a bucket. An option left unset is not sent, and the
/// service applies its default: every index, up to 500 per page, sorted by name.
/// </summary>
public sealed record ListVectorIndexesOptions
{
    /// <summary>Returns only the indexes whose name starts with this text, compared case-sensitively.</summary>
    public string? Prefix { get; init; }

    /// <summary>The maximum number of indexes on the page. The service accepts 1 to 500 and defaults to 500.</summary>
    public int? MaxResults { get; init; }

    /// <summary>The <see cref="VectorListPage{TItem}.NextToken"/> of the previous page, to read the page after it.</summary>
    public string? NextToken { get; init; }
}
