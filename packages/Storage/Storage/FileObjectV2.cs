using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage;

public class FileObjectV2
{

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("bucket_id")]
    public string? BucketId { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("last_accessed_at")]
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Object size in bytes. Unlike <see cref="Size"/>, this can represent objects larger than 2 GiB.
    /// </summary>
    [JsonPropertyName("size")]
    public long? SizeInBytes { get; set; }

    /// <summary>
    /// Object size in bytes, or null when it exceeds <see cref="int.MaxValue"/>.
    /// </summary>
    [JsonIgnore]
    [Obsolete("Use SizeInBytes instead; Size cannot represent objects larger than 2 GiB.")]
    public int? Size
    {
        get => SizeInBytes is > int.MaxValue ? null : (int?)SizeInBytes;
        set => SizeInBytes = value;
    }

    [JsonPropertyName("cache_control")]
    public string? CacheControl { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("etag")]
    public string? Etag { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
