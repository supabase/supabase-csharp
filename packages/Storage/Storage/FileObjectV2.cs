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

    [JsonPropertyName("size")]
    public long? Size { get; set; }

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
