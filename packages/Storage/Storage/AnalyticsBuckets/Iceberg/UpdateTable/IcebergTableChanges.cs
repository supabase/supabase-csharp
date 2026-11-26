using System;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.UpdateTable;

/// <summary>
/// The changes to commit to an Iceberg table: a new current schema, a new default partition spec, or both. At least
/// one of <see cref="Schema"/> and <see cref="PartitionSpec"/> must be set.
/// </summary>
public sealed record IcebergTableChanges
{
    /// <summary>
    /// The schema to make current. It is added to the table's schemas and rows are then written with it. A new column
    /// takes an identifier above <see cref="IcebergTableMetadata.LastColumnId"/>.
    /// </summary>
    public IcebergSchema? Schema { get; init; }

    /// <summary>The partition spec to make the default. It is added to the table's specs and rows are then written with it.</summary>
    public IcebergPartitionSpec? PartitionSpec { get; init; }

    /// <summary>
    /// The metadata these changes were made from. When set, the catalog only commits the changes if the table has not
    /// changed since in a way they depend on, and refuses them with status 409 otherwise. When null, the changes are
    /// committed whatever happened to the table in between.
    /// </summary>
    public IcebergTableMetadata? BasedOn { get; init; }

    private bool HasChanges() => this.Schema is not null || this.PartitionSpec is not null;

    internal void HasChangesOrThrow(string parameterName)
    {
        if (!this.HasChanges())
            throw new ArgumentException("The changes need a schema, a partition spec or both.", parameterName);
    }
}
