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
    /// The identifier the catalog gave the schema within the table, or null on a schema that was not loaded from the
    /// catalog. The catalog assigns it, so it is never sent: a loaded schema can be reused as is.
    /// </summary>
    public int? SchemaId { get; internal init; }
}
