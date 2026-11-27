using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.Common;
using Supabase.Storage.VectorBuckets.GetBucket;
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
    /// Lists the vector buckets of the project, one page at a time. Pass the <see cref="Page{T}.NextPageToken"/>
    /// of a page back in <see cref="ListVectorBucketsOptions.NextPageToken"/> to read the next one. Requires a service-role key.
    /// </summary>
    /// <param name="options">Prefix and paging options. When null, the service applies its defaults.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>One page of vector buckets, sorted by name.</returns>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because <see cref="ListVectorBucketsOptions.PageSize"/> is out of
    /// range (<c>Code</c> <c>InvalidRequest</c>, status 400).
    /// </exception>
    /// <example>
    /// <code>
    /// var options = new ListVectorBucketsOptions { Prefix = "embeddings-", PageSize = 50 };
    /// Page&lt;VectorBucketSummary&gt; page;
    /// do
    /// {
    ///     page = await storage.Vectors.ListVectorBucketsAsync(options);
    ///     foreach (var bucket in page.Items)
    ///         Console.WriteLine(bucket.VectorBucketName);
    ///     options = options with { NextPageToken = page.NextPageToken };
    /// }
    /// while (page.NextPageToken is not null);
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.list_vector_buckets")]
    Task<Page<VectorBucketSummary>> ListVectorBucketsAsync(ListVectorBucketsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the metadata of one vector bucket: its name, creation time and encryption. Requires a service-role key.
    /// </summary>
    /// <param name="vectorBucketName">The name of the vector bucket.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The vector bucket as the service describes it.</returns>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="vectorBucketName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket does not exist
    /// (<c>Code</c> <c>NotFoundException</c>, status 404).
    /// </exception>
    /// <example>
    /// <code>
    /// var bucket = await storage.Vectors.GetVectorBucketAsync("embeddings-prod");
    /// Console.WriteLine($"{bucket.VectorBucketName} created {bucket.CreatedAt}");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.get_vector_bucket")]
    Task<VectorBucketDetails> GetVectorBucketAsync(string vectorBucketName, CancellationToken cancellationToken = default);

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
