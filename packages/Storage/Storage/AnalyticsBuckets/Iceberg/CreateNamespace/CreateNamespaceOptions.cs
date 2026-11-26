using System.Collections.Generic;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.CreateNamespace;

/// <summary>
/// Optional settings for creating a namespace.
/// </summary>
public sealed record CreateNamespaceOptions
{
    /// <summary>
    /// The properties to store against the namespace. Without them the namespace is created with none.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
