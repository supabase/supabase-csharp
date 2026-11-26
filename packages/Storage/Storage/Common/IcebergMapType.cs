namespace Supabase.Storage.Common;

/// <summary>
/// A nested Iceberg type mapping keys of one type to values of another.
/// </summary>
public sealed record IcebergMapType : IcebergType
{
    /// <summary>The field identifier of the keys, unique across the whole schema.</summary>
    public required int KeyId { get; init; }

    /// <summary>The type of the keys.</summary>
    public required IcebergType Key { get; init; }

    /// <summary>The field identifier of the values, unique across the whole schema.</summary>
    public required int ValueId { get; init; }

    /// <summary>The type of the values.</summary>
    public required IcebergType Value { get; init; }

    /// <summary>Whether every value must be present.</summary>
    public required bool ValueRequired { get; init; }
}
