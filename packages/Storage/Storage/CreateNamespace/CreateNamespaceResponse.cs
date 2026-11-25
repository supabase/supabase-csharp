using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.CreateNamespace;

internal sealed class CreateNamespaceResponse
{
    [JsonPropertyName("namespace")]
    public string[]? Namespace { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, string>? Properties { get; set; }

    internal IcebergNamespaceDetails ToDetails(IcebergNamespace requested) => new()
    {
        Namespace = this.Namespace is { Length: > 0 } levels ? IcebergNamespace.Of(levels) : requested,
        Properties = this.Properties ?? new Dictionary<string, string>(),
    };
}
