using System;
using System.Collections.Generic;

namespace Supabase.Storage.Common;

/// <summary>
/// One page of a list the service returns a page at a time. Pass its <see cref="NextPageToken"/> back in the list
/// options to read the page after it.
/// </summary>
/// <typeparam name="T">The type of the listed items.</typeparam>
public sealed record Page<T>
{
    /// <summary>The items of this page, in the order the service listed them. Empty when there are none.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>The token that requests the next page. Null means this is the last page.</summary>
    public string? NextPageToken { get; init; }
}
