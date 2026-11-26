using System;
using System.Linq;
using System.Text.Json.Serialization;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.ListTables;

internal sealed class ListTablesResponse
{
    [JsonPropertyName("identifiers")]
    public IdentifierBody[]? Identifiers { get; set; }

    [JsonPropertyName("next-page-token")]
    public string? NextPageToken { get; set; }

    internal Page<IcebergTable> ToPage(IcebergNamespace listed) => new()
    {
        Items = (this.Identifiers ?? Array.Empty<IdentifierBody>()).Select(identifier => identifier.ToTable(listed)).ToList(),
        NextPageToken = this.NextPageToken,
    };

    internal sealed class IdentifierBody
    {
        [JsonPropertyName("namespace")]
        public string[]? Namespace { get; set; }

        [JsonRequired]
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        internal IcebergTable ToTable(IcebergNamespace listed) =>
            (this.Namespace is { Length: > 0 } levels ? IcebergNamespace.Of(levels) : listed).Table(this.Name);
    }
}
