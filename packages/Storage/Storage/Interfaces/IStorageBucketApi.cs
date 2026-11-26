using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Interfaces;
using Supabase.Storage.FileBuckets.Common;

namespace Supabase.Storage.Interfaces;

public interface IStorageBucketApi<TBucket> : IGettableHeaders
    where TBucket : Bucket
{
    ClientOptions Options { get; }
    Dictionary<string, string> Headers { get; set; }

    Task<string> CreateBucket(string id, BucketUpsertOptions? options = null);
    Task<GenericResponse?> DeleteBucket(string id);
    Task<GenericResponse?> EmptyBucket(string id);
    Task<TBucket?> GetBucket(string id);
    Task<List<TBucket>?> ListBuckets();
    Task<TBucket?> UpdateBucket(string id, BucketUpsertOptions? options = null);

    /// <summary>
    /// Purges the CDN cache for every object in a bucket. Requires a service-role key.
    /// </summary>
    /// <param name="id">The bucket whose cached objects should be purged.</param>
    /// <param name="options">
    /// When <see cref="PurgeCacheOptions.Transformations"/> is <c>true</c>, only the transformed
    /// variants are purged; otherwise every cached version is purged.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The service acknowledgement of the purge.</returns>
    /// <example>
    /// <code>
    /// await storage.PurgeBucketCache("avatars", new PurgeCacheOptions { Transformations = true });
    /// </code>
    /// </example>
    Task<GenericResponse?> PurgeBucketCache(string id, PurgeCacheOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges the CDN cache for every object in every bucket of the project. Requires a service-role key.
    /// </summary>
    /// <example>
    /// <code>
    /// await storage.PurgeProjectCacheAsync();
    /// </code>
    /// </example>
    Task PurgeProjectCacheAsync(PurgeCacheOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the lifecycle policy from a bucket. Succeeds even when no policy is stored.
    /// </summary>
    /// <example>
    /// <code>
    /// await storage.DeleteBucketLifecycleAsync("avatars");
    /// </code>
    /// </example>
    Task DeleteBucketLifecycleAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the lifecycle policy stored on a bucket. Throws when no policy is stored.
    /// </summary>
    /// <example>
    /// <code>
    /// var lifecycle = await storage.GetBucketLifecycleAsync("avatars");
    /// </code>
    /// </example>
    Task<BucketLifecycle> GetBucketLifecycleAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the lifecycle policy of a bucket and returns it as stored, with the rule ids the service generated.
    /// </summary>
    /// <example>
    /// <code>
    /// var stored = await storage.UpdateBucketLifecycleAsync("avatars", new BucketLifecycle
    /// {
    ///     Rules = new[]
    ///     {
    ///         new LifecycleRule
    ///         {
    ///             Status = LifecycleRuleStatus.Enabled,
    ///             NoncurrentVersionExpiration = new NoncurrentVersionExpiration { NoncurrentDays = 30 },
    ///         },
    ///     },
    /// });
    /// </code>
    /// </example>
    Task<BucketLifecycle> UpdateBucketLifecycleAsync(string id, BucketLifecycle lifecycle, CancellationToken cancellationToken = default);
}