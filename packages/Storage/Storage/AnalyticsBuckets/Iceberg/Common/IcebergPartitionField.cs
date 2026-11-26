namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// A partition field of an Iceberg table: a column, and the transform that turns its values into partition values.
/// </summary>
public sealed record IcebergPartitionField
{
    /// <summary>The <see cref="IcebergField.Id"/> of the column the partition values come from.</summary>
    public required int SourceId { get; init; }

    /// <summary>The name of the partition field.</summary>
    public required string Name { get; init; }

    /// <summary>The transform applied to the column's values.</summary>
    public required IcebergTransform Transform { get; init; }

    /// <summary>
    /// The identifier of the partition field, unique across all the specs of the table. Leave it unset on a new field
    /// and the catalog assigns one above <see cref="IcebergTableMetadata.LastPartitionId"/>. Keep it on a field taken
    /// from a loaded spec: the Iceberg table spec requires an equivalent field to keep its identifier. It is only sent
    /// when set.
    /// </summary>
    public int? FieldId { get; init; }
}
