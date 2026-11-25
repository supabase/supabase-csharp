using System;
using System.Collections.Generic;

namespace Supabase.Storage.ListVectorIndexes;

/// <summary>
/// One page of a list the vector service returns a page at a time.
/// </summary>
/// <typeparam name="TItem">The type of the listed items.</typeparam>
public sealed record VectorListPage<TItem>
{
    /// <summary>The items of this page, in the order the service listed them.</summary>
    public IReadOnlyList<TItem> Items { get; init; } = Array.Empty<TItem>();

    /// <summary>The token that requests the next page, or null when this page is the last.</summary>
    public string? NextToken { get; init; }
}
