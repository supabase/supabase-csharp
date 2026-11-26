namespace Supabase.Storage.Common;

/// <summary>
/// A column of an Iceberg table, or a field of a nested struct.
/// </summary>
public sealed record IcebergField
{
    /// <summary>The identifier of the field, unique across the whole schema, nested fields included.</summary>
    public required int Id { get; init; }

    /// <summary>The name of the field.</summary>
    public required string Name { get; init; }

    /// <summary>The type of the values the field holds.</summary>
    public required IcebergType Type { get; init; }

    /// <summary>Whether every row must hold a value for the field.</summary>
    public required bool Required { get; init; }

    /// <summary>A description of the field, or null when there is none.</summary>
    public string? Doc { get; init; }
}
