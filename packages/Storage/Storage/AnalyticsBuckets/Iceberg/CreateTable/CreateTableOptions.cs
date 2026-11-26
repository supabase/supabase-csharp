using System.Collections.Generic;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.CreateTable;

/// <summary>
/// Optional settings for creating an Iceberg table.
/// </summary>
public sealed record CreateTableOptions
{
    /// <summary>How to partition the table's rows. Without it, the catalog creates the table unpartitioned.</summary>
    public IcebergPartitionSpec? PartitionSpec { get; init; }

    /// <summary>The properties to store against the table. Without them the table is created with the catalog's defaults.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
