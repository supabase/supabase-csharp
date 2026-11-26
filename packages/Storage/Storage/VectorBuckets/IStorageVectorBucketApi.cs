using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.VectorBuckets.Common;
using Supabase.Storage.VectorBuckets.ListBuckets;

namespace Supabase.Storage.VectorBuckets;

/// <summary>
/// Operations on vector buckets, which hold the vector indexes of the Storage vector service.
/// </summary>
public interface IStorageVectorBucketApi
{
    /// <summary>
    /// Scopes the following operations to one vector bucket. Nothing is sent to the service.
    /// </summary>
    /// <param name="vectorBucketName">The name of the vector bucket.</param>
    /// <returns>A client for the operations that act inside that bucket.</returns>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="vectorBucketName"/> is null, empty or whitespace.
    /// </exception>
    /// <example>
    /// <code>
    /// var bucket = storage.Vectors.From("embeddings-prod");
    /// </code>
    /// </example>
    IVectorBucket From(string vectorBucketName);

    /// <summary>
    /// Creates a vector bucket, which holds vector indexes. Requires a service-role key.
    /// </summary>
    /// <param name="vectorBucketName">The name of the vector bucket to create. The service validates it.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="vectorBucketName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket already exists
    /// (<c>Code</c> <c>ConflictException</c>, status 409) or the project reached its bucket quota
    /// (<c>Code</c> <c>S3VectorMaxBucketsExceeded</c>, status 400).
    /// </exception>
    /// <example>
    /// <code>
    /// await storage.Vectors.CreateVectorBucketAsync("embeddings-prod");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.create_vector_bucket")]
    Task CreateVectorBucketAsync(string vectorBucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the vector buckets of the project, one page at a time. Pass the <see cref="VectorListPage{TItem}.NextToken"/>
    /// of a page back in <see cref="ListVectorBucketsOptions.NextToken"/> to read the next one. Requires a service-role key.
    /// </summary>
    /// <param name="options">Prefix and paging options. When null, the service applies its defaults.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>One page of vector buckets, sorted by name.</returns>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because <see cref="ListVectorBucketsOptions.MaxResults"/> is out of
    /// range (<c>Code</c> <c>InvalidRequest</c>, status 400).
    /// </exception>
    /// <example>
    /// <code>
    /// var options = new ListVectorBucketsOptions { Prefix = "embeddings-", MaxResults = 50 };
    /// var page = await storage.Vectors.ListVectorBucketsAsync(options);
    /// while (page.NextToken is not null)
    ///     page = await storage.Vectors.ListVectorBucketsAsync(options with { NextToken = page.NextToken });
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.list_vector_buckets")]
    Task<VectorListPage<VectorBucketSummary>> ListVectorBucketsAsync(ListVectorBucketsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an empty vector bucket. The service rejects the call while the bucket still has
    /// indexes, and accepts it for a bucket that does not exist. Requires a service-role key.
    /// </summary>
    /// <param name="vectorBucketName">The name of the vector bucket to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="vectorBucketName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket still has indexes
    /// (<c>Code</c> <c>VectorBucketNotEmpty</c>, status 400).
    /// </exception>
    /// <example>
    /// <code>
    /// await storage.Vectors.DeleteVectorBucketAsync("embeddings-prod");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.delete_vector_bucket")]
    Task DeleteVectorBucketAsync(string vectorBucketName, CancellationToken cancellationToken = default);
}
