using System.Collections.Generic;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// A nested Iceberg type made of named fields.
/// </summary>
public sealed record IcebergStructType : IcebergType
{
    /// <summary>The fields of the struct, in order.</summary>
    public required IReadOnlyList<IcebergField> Fields { get; init; }
}
