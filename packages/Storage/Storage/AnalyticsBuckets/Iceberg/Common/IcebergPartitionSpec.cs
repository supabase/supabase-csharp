using System.Collections.Generic;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// How an Iceberg table splits its rows into partitions. A spec without fields leaves the table unpartitioned.
/// </summary>
public sealed record IcebergPartitionSpec
{
    /// <summary>The partition fields, in order.</summary>
    public required IReadOnlyList<IcebergPartitionField> Fields { get; init; }

    /// <summary>
    /// The identifier the catalog gave the spec within the table, or null on a spec that was not loaded from the
    /// catalog. The catalog assigns it, so it is never sent: a loaded spec can be reused as is.
    /// </summary>
    public int? SpecId { get; internal init; }
}
