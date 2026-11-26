using System;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListNamespaces;

internal sealed class ListNamespacesResponse
{
    [JsonPropertyName("namespaces")]
    public string[][]? Namespaces { get; set; }

    [JsonPropertyName("next-page-token")]
    public string? NextPageToken { get; set; }

    internal Page<IcebergNamespace> ToPage() => new()
    {
        Items = (this.Namespaces ?? Array.Empty<string[]>()).Select(levels => IcebergNamespace.Of(levels)).ToList(),
        NextPageToken = this.NextPageToken,
    };
}
