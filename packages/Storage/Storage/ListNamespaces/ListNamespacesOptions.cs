using Supabase.Storage.Common;

namespace Supabase.Storage.ListNamespaces;

/// <summary>
/// Options for listing namespaces. An option left unset is not sent. They follow the Iceberg REST Catalog spec;
/// Storage ignores all of them today and lists every namespace of the bucket.
/// </summary>
public sealed record ListNamespacesOptions
{
    /// <summary>Lists only the namespaces nested under this one. Without it, the top-level namespaces are listed.</summary>
    public IcebergNamespace? Parent { get; init; }

    /// <summary>The maximum number of namespaces to return in one page.</summary>
    public int? PageSize { get; init; }

    /// <summary>The <see cref="IcebergNamespacePage.NextPageToken"/> of the previous page, to read the next one.</summary>
    public string? PageToken { get; init; }
}
