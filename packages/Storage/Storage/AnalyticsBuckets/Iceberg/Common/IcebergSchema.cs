using System.Collections.Generic;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// The schema of an Iceberg table: the columns it stores.
/// </summary>
public sealed record IcebergSchema
{
    /// <summary>The columns of the table, in order.</summary>
    public required IReadOnlyList<IcebergField> Fields { get; init; }

    /// <summary>
    /// The identifier of the schema within the table. The catalog assigns it: leave it unset when defining a schema,
    /// and it is only sent when set.
    /// </summary>
    public int? SchemaId { get; init; }
}
