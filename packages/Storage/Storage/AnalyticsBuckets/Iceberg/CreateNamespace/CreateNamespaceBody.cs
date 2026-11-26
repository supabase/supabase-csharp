using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Supabase.Storage.AnalyticsBuckets.Iceberg.CreateNamespace;

internal sealed class CreateNamespaceBody
{
    private CreateNamespaceBody(IReadOnlyList<string> @namespace, IReadOnlyDictionary<string, string>? properties)
    {
        this.Namespace = @namespace;
        this.Properties = properties;
    }

    [JsonPropertyName("namespace")]
    public IReadOnlyList<string> Namespace { get; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Properties { get; }

    internal static CreateNamespaceBody For(IcebergNamespace @namespace, CreateNamespaceOptions? options)
    {
        var properties = options?.Properties;
        return new CreateNamespaceBody(@namespace.Levels, properties is { Count: > 0 } ? properties : null);
    }
}
