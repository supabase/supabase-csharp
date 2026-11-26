using Supabase.Storage.Common;
using Supabase.Storage.VectorBuckets.Common;

namespace Supabase.Storage.VectorBuckets.ListVectors;

/// <summary>
/// Paging, content and segment options for listing the vectors of an index. An option left unset is not sent, and the
/// service applies its default: every vector, keys only, up to 500 per page.
/// </summary>
public sealed record ListVectorsOptions
{
    /// <summary>The maximum number of vectors on the page. The service accepts 1 to 1000.</summary>
    public int? PageSize { get; init; }

    /// <summary>The <see cref="Page{T}.NextPageToken"/> of the previous page, to read the page after it.</summary>
    public string? NextPageToken { get; init; }

    /// <summary>What to return with each key. With <see cref="VectorContent.None"/>, the service applies its own default.</summary>
    public VectorContent Include { get; init; } = VectorContent.None;

    /// <summary>The part of the index to list, to split a full scan across parallel callers. The whole index when null.</summary>
    public VectorSegment? Segment { get; init; }
}
