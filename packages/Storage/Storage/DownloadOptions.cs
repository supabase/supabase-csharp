using Supabase.Core.Attributes;

namespace Supabase.Storage;

public class DownloadOptions
{
    /// <summary>
    ///    <p>Use the original file name when downloading</p>
    /// </summary>
    public static readonly DownloadOptions UseOriginalFileName = new DownloadOptions { FileName = "" };

    /// <summary>
    ///     <p>The name of the file to be downloaded</p>
    ///     <p>When field is null, no download attribute will be added.</p>
    ///     <p>When field is empty, the original file name will be used. Use <see cref="UseOriginalFileName"/> for quick initialized with original file names.</p>
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Append a cache nonce parameter to the URL to invalidate the cache
    /// </summary>
    [ImplementedCapability("storage.file_buckets.url_cache_nonce")]
    public string? CacheNonce { get; set; }
}
