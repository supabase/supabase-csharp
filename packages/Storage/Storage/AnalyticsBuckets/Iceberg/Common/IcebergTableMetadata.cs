using System;
using System.Collections.Generic;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// The metadata of an Iceberg table as the catalog stores it.
/// </summary>
public sealed record IcebergTableMetadata
{
    /// <summary>The location of the metadata file this metadata was read from, or null when the catalog did not report it.</summary>
    public string? MetadataLocation { get; init; }

    /// <summary>The identifier the catalog gave the table when it was created.</summary>
    public required Guid TableUuid { get; init; }

    /// <summary>The version of the Iceberg table format.</summary>
    public required int FormatVersion { get; init; }

    /// <summary>The base location of the table's files.</summary>
    public required string Location { get; init; }

    /// <summary>When the metadata was last updated.</summary>
    public required DateTimeOffset LastUpdatedAt { get; init; }

    /// <summary>The schema rows are currently written with.</summary>
    public required IcebergSchema CurrentSchema { get; init; }

    /// <summary>The partition spec rows are currently written with.</summary>
    public required IcebergPartitionSpec DefaultPartitionSpec { get; init; }

    /// <summary>The highest field identifier assigned so far. A new column takes a higher one.</summary>
    public required int LastColumnId { get; init; }

    /// <summary>The highest partition field identifier assigned so far.</summary>
    public required int LastPartitionId { get; init; }

    /// <summary>The properties of the table. Empty when there are none.</summary>
    public required IReadOnlyDictionary<string, string> Properties { get; init; }
}
