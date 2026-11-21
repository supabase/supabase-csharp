using Supabase.Core.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Supabase.Storage.Interfaces
{
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
	}
}