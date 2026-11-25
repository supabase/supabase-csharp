using System;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.Common;

namespace Supabase.Storage.ListNamespaces;

internal sealed class ListNamespacesResponse
{
    [JsonPropertyName("namespaces")]
    public string[][]? Namespaces { get; set; }

    [JsonPropertyName("next-page-token")]
    public string? NextPageToken { get; set; }

    internal IcebergNamespacePage ToPage() => new()
    {
        Namespaces = (this.Namespaces ?? Array.Empty<string[]>()).Select(levels => IcebergNamespace.Of(levels)).ToList(),
        NextPageToken = this.NextPageToken,
    };
}
