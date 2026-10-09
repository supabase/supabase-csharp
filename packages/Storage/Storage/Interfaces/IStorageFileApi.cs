using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Supabase.Storage.Interfaces
{
    public interface IStorageFileApi<TFileObject>
        where TFileObject : FileObject
    {
        ClientOptions Options { get; }
        Task<string> CreateSignedUrl(
            string path,
            int expiresIn,
            TransformOptions? transformOptions = null,
            DownloadOptions? options = null
        );
        Task<List<CreateSignedUrlsResponse>?> CreateSignedUrls(
            List<string> paths,
            int expiresIn,
            DownloadOptions? options = null
        );
        Task<byte[]> Download(string supabasePath, EventHandler<float>? onProgress = null, CancellationToken cancellationToken = default, string? cacheNonce = null);
        Task<byte[]> Download(
            string supabasePath,
            TransformOptions? transformOptions = null,
            EventHandler<float>? onProgress = null,
            CancellationToken cancellationToken = default,
            string? cacheNonce = null
        );
        Task<string> Download(
            string supabasePath,
            string localPath,
            EventHandler<float>? onProgress = null,
            CancellationToken cancellationToken = default,
            string? cacheNonce = null
        );
        Task<string> Download(
            string supabasePath,
            string localPath,
            TransformOptions? transformOptions = null,
            EventHandler<float>? onProgress = null,
            CancellationToken cancellationToken = default,
            string? cacheNonce = null
        );
        Task<byte[]> DownloadPublicFile(
            string supabasePath,
            TransformOptions? transformOptions = null,
            EventHandler<float>? onProgress = null,
            CancellationToken cancellationToken = default,
            string? cacheNonce = null
        );
        Task<string> DownloadPublicFile(
            string supabasePath,
            string localPath,
            TransformOptions? transformOptions = null,
            EventHandler<float>? onProgress = null,
            CancellationToken cancellationToken = default,
            string? cacheNonce = null
        );
        string GetPublicUrl(
            string path,
            TransformOptions? transformOptions = null,
            DownloadOptions? options = null
        );
        Task<List<TFileObject>?> List(string path = "", SearchOptions? options = null);
        /// <summary>Lists the objects under a prefix, one page at a time.</summary>
        Task<SearchV2Result?> ListV2Async(SearchV2Options? options = null, CancellationToken cancellationToken = default);
        Task<FileObjectV2?> Info(string path);
        Task<bool> Move(string fromPath, string toPath, DestinationOptions? options = null);
        Task<bool> Copy(string fromPath, string toPath, DestinationOptions? options = null);
        Task<TFileObject?> Remove(string path);
        Task<List<TFileObject>?> Remove(List<string> paths);
        Task<string> Update(
            byte[] data,
            string supabasePath,
            FileOptions? options = null,
            EventHandler<float>? onProgress = null
        );
        Task<string> Update(
            string localFilePath,
            string supabasePath,
            FileOptions? options = null,
            EventHandler<float>? onProgress = null
        );
        Task<string> Upload(
            byte[] data,
            string supabasePath,
            FileOptions? options = null,
            EventHandler<float>? onProgress = null,
            bool inferContentType = true,
            CancellationToken cancellationToken = default
        );
        Task<string> Upload(
            string localFilePath,
            string supabasePath,
            FileOptions? options = null,
            EventHandler<float>? onProgress = null,
            bool inferContentType = true,
            CancellationToken cancellationToken = default
        );
        Task UploadOrResume(
            string localPath,
            string fileName,
            FileOptions options,
            EventHandler<float>? onProgress = null,
            CancellationToken cancellationToken = default
        );
        Task UploadOrResume(
            byte[] data,
            string fileName,
            FileOptions options,
            EventHandler<float>? onProgress = null,
            CancellationToken cancellationToken = default
        );
        Task<string> UploadToSignedUrl(
            byte[] data,
            UploadSignedUrl url,
            FileOptions? options = null,
            EventHandler<float>? onProgress = null,
            bool inferContentType = true
        );
        Task<string> UploadToSignedUrl(
            string localFilePath,
            UploadSignedUrl url,
            FileOptions? options = null,
            EventHandler<float>? onProgress = null,
            bool inferContentType = true
        );
        Task<UploadSignedUrl> CreateUploadSignedUrl(string supabasePath);

        /// <summary>
        /// Purges the CDN cache for a single object. Requires a service-role key.
        /// </summary>
        /// <param name="path">The object path within the bucket to purge.</param>
        /// <param name="options">
        /// When <see cref="PurgeCacheOptions.Transformations"/> is <c>true</c>, only the transformed
        /// variants are purged; otherwise every cached version is purged.
        /// </param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The service acknowledgement of the purge.</returns>
        /// <example>
        /// <code>
        /// await storage.From("avatars").PurgeCache("folder/avatar.png");
        /// </code>
        /// </example>
        Task<GenericResponse?> PurgeCache(string path, PurgeCacheOptions? options = null, CancellationToken cancellationToken = default);
    }
}

