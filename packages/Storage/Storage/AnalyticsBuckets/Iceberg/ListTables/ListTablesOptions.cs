using Supabase.Storage.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListTables;

/// <summary>
/// Options for listing the tables of a namespace. An option left unset is not sent. They follow the Iceberg REST
/// Catalog spec; Storage applies <see cref="PageSize"/> as a limit and ignores <see cref="NextPageToken"/> today.
/// </summary>
public sealed record ListTablesOptions
{
    /// <summary>The maximum number of tables to return in one page.</summary>
    public int? PageSize { get; init; }

    /// <summary>The <see cref="Page{T}.NextPageToken"/> of the previous page, to read the next one.</summary>
    public string? NextPageToken { get; init; }
}
