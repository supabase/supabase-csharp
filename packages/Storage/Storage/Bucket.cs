using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Supabase.Storage;

public class Bucket
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

    /// <summary>
    /// The visibility of the bucket. Public buckets don't require an authorization token to download objects,
    /// but still require a valid token for all other operations. By default, buckets are private.
    /// </summary>
    [JsonPropertyName("public")]
    public bool Public { get; set; }

    /// <summary>
    /// Maximum allowed object size in bytes, or null when the bucket has no limit.
    /// </summary>
    [JsonPropertyName("file_size_limit")]
    public long? FileSizeLimitInBytes { get; set; }

    /// <summary>
    /// Maximum allowed object size in bytes as a string, or null when the bucket has no limit.
    /// </summary>
    /// <remarks>
    /// Non-numeric strings assigned to this property are mapped to null.
    /// </remarks>
    [JsonIgnore]
    [Obsolete("Use FileSizeLimitInBytes instead. FileSizeLimit will be removed in the next major version.")]
    public string? FileSizeLimit
    {
        get => this.FileSizeLimitInBytes?.ToString(CultureInfo.InvariantCulture);
        set => this.FileSizeLimitInBytes = long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Specifies the allowed mime types that this bucket can accept during upload.
    ///
    /// Expects a List of values such as: ['image/jpeg', 'image/png', etc]
    /// </summary>
    [JsonPropertyName("allowed_mime_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedMimes { get; set; }
}
