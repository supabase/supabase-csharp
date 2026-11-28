using System.Text.Json.Serialization;
using Supabase.Core.Attributes;
using Supabase.Storage.FileBuckets.Common;

namespace Supabase.Storage;

/// <summary>
/// Options for <see cref="StorageFileApi.ListV2Async"/>.
/// </summary>
public class SearchV2Options
{
    /// <summary>
    /// Only objects whose name starts with this prefix, e.g. "folder/".
    /// </summary>
    [JsonPropertyName("prefix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prefix { get; set; }

    /// <summary>
    /// Number of entries per page. The server defaults to 1000 and caps it there.
    /// </summary>
    [JsonPropertyName("limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; set; }

    /// <summary>
    /// The <see cref="SearchV2Result.NextCursor"/> of the previous page.
    /// </summary>
    [JsonPropertyName("cursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cursor { get; set; }

    /// <summary>
    /// Group sub-folders into <see cref="SearchV2Result.Folders"/> instead of listing nested objects.
    /// </summary>
    [JsonPropertyName("with_delimiter")]
    public bool WithDelimiter { get; set; }

    /// <summary>
    /// Column to sort by: name, updated_at or created_at.
    /// </summary>
    [JsonPropertyName("sortBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SortBy? SortBy { get; set; }

    /// <summary>
    /// Whether earlier versions of objects are listed. Not sent when null, and the service then leaves them out.
    /// </summary>
    [ImplementedCapability("storage.file_buckets.list_file_versions")]
    [JsonPropertyName("noncurrentVersions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ListingFilter? NoncurrentVersions { get; set; }

    /// <summary>
    /// Whether the markers left by deleting a versioned object are listed. Not sent when null, and the service then leaves them out.
    /// </summary>
    [ImplementedCapability("storage.file_buckets.list_file_versions")]
    [JsonPropertyName("deleteMarkers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ListingFilter? DeleteMarkers { get; set; }
}
