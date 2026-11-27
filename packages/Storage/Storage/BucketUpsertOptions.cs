using System.Collections.Generic;
using System.Text.Json.Serialization;
using Supabase.Core.Attributes;
using Supabase.Storage.FileBuckets.Common;

namespace Supabase.Storage;

public class BucketUpsertOptions
{
    /// <summary>
    /// The visibility of the bucket. Public buckets don't require an authorization token to download objects,
    /// but still require a valid token for all other operations. By default, buckets are private.
    /// </summary>
    [JsonPropertyName("public")]
    public bool Public { get; set; } = false;

    /// <summary>
    /// Specifies the file size limit that this bucket can accept during upload.
    ///
    /// Expects a string value following a format like: '1kb', '50mb', '150kb', etc.
    /// </summary>
    [JsonPropertyName("file_size_limit")]
    public string? FileSizeLimit { get; set; }

    /// <summary>
    /// Specifies the allowed mime types that this bucket can accept during upload.
    ///
    /// Expects a List of values such as: ['image/jpeg', 'image/png', etc]
    /// </summary>
    [JsonPropertyName("allowed_mime_types")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedMimes { get; set; }

    /// <summary>
    /// The versioning status to create the bucket with, or to move it to on an update. Not sent when null.
    /// </summary>
    [ImplementedCapability("storage.file_buckets.bucket_versioning_status")]
    public BucketVersioningStatus? VersioningStatus { get; set; }
}
