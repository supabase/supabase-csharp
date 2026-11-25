using System;
using System.Text.Json.Serialization;

namespace Supabase.Storage.Common;

/// <summary>
/// An analytics bucket, the Apache Iceberg backed kind of Supabase Storage bucket.
/// </summary>
public sealed record AnalyticsBucket
{
    /// <summary>The name of the bucket, unique within the project.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>When the bucket was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the bucket was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}
