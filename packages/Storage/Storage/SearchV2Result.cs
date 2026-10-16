using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Supabase.Storage;

/// <summary>
/// One page of <see cref="StorageFileApi.ListV2Async"/>.
/// </summary>
public class SearchV2Result
{
    /// <summary>
    /// Whether there are more pages.
    /// </summary>
    [JsonPropertyName("hasNext")]
    public bool HasNext { get; set; }

    /// <summary>
    /// Pass it as <see cref="SearchV2Options.Cursor"/> for the next page.
    /// </summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    /// <summary>
    /// Sub-folders, only when listing with a delimiter.
    /// </summary>
    [JsonPropertyName("folders")]
    public List<FileObject> Folders { get; set; } = new();

    /// <summary>
    /// The objects on this page. Without a delimiter the name is the full path.
    /// </summary>
    [JsonPropertyName("objects")]
    public List<FileObject> Objects { get; set; } = new();
}
