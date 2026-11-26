namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// A nested Iceberg type holding a list of elements of one type.
/// </summary>
public sealed record IcebergListType : IcebergType
{
    /// <summary>The field identifier of the elements, unique across the whole schema.</summary>
    public required int ElementId { get; init; }

    /// <summary>The type of the elements.</summary>
    public required IcebergType Element { get; init; }

    /// <summary>Whether every element must hold a value.</summary>
    public required bool ElementRequired { get; init; }
}
