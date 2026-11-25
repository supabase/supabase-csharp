using System.Collections.Generic;
using Supabase.Storage.Common;

namespace Supabase.Storage.CreateNamespace;

/// <summary>
/// A namespace as the service stores it.
/// </summary>
public sealed record IcebergNamespaceDetails
{
    /// <summary>The identifier of the namespace.</summary>
    public required IcebergNamespace Namespace { get; init; }

    /// <summary>The properties stored against the namespace. Empty when there are none.</summary>
    public required IReadOnlyDictionary<string, string> Properties { get; init; }
}
