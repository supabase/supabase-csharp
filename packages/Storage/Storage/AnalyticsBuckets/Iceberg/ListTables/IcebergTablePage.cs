using System;
using System.Collections.Generic;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListTables;

/// <summary>
/// One page of the tables of a namespace.
/// </summary>
public sealed record IcebergTablePage
{
    /// <summary>The tables of this page, in the order the service listed them.</summary>
    public IReadOnlyList<IcebergTable> Tables { get; init; } = Array.Empty<IcebergTable>();

    /// <summary>The token that requests the next page, or null when this page is the last.</summary>
    public string? NextPageToken { get; init; }
}
