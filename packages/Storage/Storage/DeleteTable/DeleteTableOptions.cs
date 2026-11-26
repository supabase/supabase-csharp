namespace Supabase.Storage.DeleteTable;

/// <summary>
/// Optional settings for deleting an Iceberg table. An option left unset is not sent, and the service applies its default.
/// </summary>
public sealed record DeleteTableOptions
{
    /// <summary>
    /// Whether to also delete the table's data and metadata files. When unset, the service decides; its default today
    /// is false, which removes the table from the catalog and leaves its files in place.
    /// </summary>
    public bool? Purge { get; init; }
}
