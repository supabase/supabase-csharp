using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage;

/// <summary>
/// Request body for bucket create/update. Separate from Bucket so the obsolete Bucket.FileSizeLimit can stay response-only.
/// </summary>
internal sealed class BucketRequest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("owner")]
    public string? Owner { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("public")]
    public bool Public { get; set; }

    [JsonPropertyName("file_size_limit")]
    public string? FileSizeLimit { get; set; }

    [JsonPropertyName("allowed_mime_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedMimes { get; set; }
}
