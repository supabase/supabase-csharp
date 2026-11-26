using System;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListNamespaces;

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
