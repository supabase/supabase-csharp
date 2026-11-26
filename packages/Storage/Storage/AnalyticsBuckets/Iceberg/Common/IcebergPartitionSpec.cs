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
    /// The identifier of the spec within the table. The catalog assigns it: leave it unset when defining a spec,
    /// and it is only sent when set.
    /// </summary>
    public int? SpecId { get; init; }
}
