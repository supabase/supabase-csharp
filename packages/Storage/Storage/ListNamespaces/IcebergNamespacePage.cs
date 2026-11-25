using System;
using System.Collections.Generic;
using Supabase.Storage.Common;

namespace Supabase.Storage.ListNamespaces;

/// <summary>
/// One page of the namespaces of an analytics bucket.
/// </summary>
public sealed record IcebergNamespacePage
{
    /// <summary>The namespaces of this page, in the order the service listed them.</summary>
    public IReadOnlyList<IcebergNamespace> Namespaces { get; init; } = Array.Empty<IcebergNamespace>();

    /// <summary>The token that requests the next page, or null when this page is the last.</summary>
    public string? NextPageToken { get; init; }
}
