using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage;

public class FileObject
{
    /// <summary>
    /// Flag representing if this object is a folder, all properties will be null but the name
    /// </summary>
    public bool IsFolder => !string.IsNullOrEmpty(this.Name) && this.Id == null && this.CreatedAt == null && this.UpdatedAt == null;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The name relative to the listed prefix. Null unless listed with a delimiter.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("bucket_id")]
    public string? BucketId { get; set; }

    [JsonPropertyName("owner")]
    public string? Owner { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("last_accessed_at")]
    public DateTime? LastAccessedAt { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> MetaData = new Dictionary<string, object>();

    [JsonPropertyName("buckets")]
    public Bucket? Buckets { get; set; }
}
