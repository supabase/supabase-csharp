using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.GetVectorIndex;
using Supabase.Storage.ListVectorIndexes;

namespace Supabase.Storage.Interfaces;

/// <summary>
/// Operations scoped to one vector bucket, obtained from <see cref="IStorageVectorBucketApi.From"/>.
/// </summary>
public interface IVectorBucket
{
    /// <summary>
    /// Scopes the following operations to one vector index of this bucket. Nothing is sent to the service.
    /// </summary>
    /// <param name="indexName">The name of the vector index.</param>
    /// <returns>A client for the operations that act inside that index.</returns>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="indexName"/> is null, empty or whitespace.
    /// </exception>
    /// <example>
    /// <code>
    /// var index = storage.Vectors.From("embeddings-prod").Index("docs-v1");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.access_vector_index")]
    IVectorIndex Index(string indexName);

    /// <summary>
    /// Creates a vector index in this bucket. Requires a service-role key.
    /// </summary>
    /// <param name="index">The settings of the index to create.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="index"/> is null.</exception>
    /// <exception cref="System.ArgumentException">
    /// <see cref="NewVectorIndex.IndexName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket does not exist
    /// (<c>Code</c> <c>S3VectorNotFoundException</c>) or the settings are invalid (status 400).
    /// </exception>
    /// <example>
    /// <code>
    /// await storage.Vectors.From("embeddings-prod").CreateIndexAsync(new NewVectorIndex
    /// {
    ///     IndexName = "docs-v1",
    ///     Dimension = 1536,
    ///     DistanceMetric = VectorDistanceMetric.Cosine,
    ///     NonFilterableMetadataKeys = new[] { "raw_text" },
    /// });
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.create_index")]
    Task CreateIndexAsync(NewVectorIndex index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the settings and creation time of a vector index of this bucket. Requires a service-role key.
    /// </summary>
    /// <param name="indexName">The name of the index to describe.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The index as the service describes it.</returns>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="indexName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket or the index does not exist
    /// (<c>Code</c> <c>NotFoundException</c>), or its answer could not be read.
    /// </exception>
    /// <example>
    /// <code>
    /// var index = await storage.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1");
    /// if (index.DistanceMetric == VectorDistanceMetric.Cosine)
    /// {
    ///     Console.WriteLine($"{index.IndexName} holds {index.Dimension}-dimensional vectors");
    /// }
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.get_index")]
    Task<VectorIndexDetails> GetIndexAsync(string indexName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the vector indexes of this bucket a page at a time, sorted by name. A bucket that does not exist lists
    /// no index. Requires a service-role key.
    /// </summary>
    /// <param name="options">The name prefix and paging to apply. Null lists the first page of every index.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>A page of indexes, and the token of the next page when there is one.</returns>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because <see cref="ListVectorIndexesOptions.MaxResults"/> is out of
    /// range (<c>Code</c> <c>InvalidRequest</c>), or its answer could not be read.
    /// </exception>
    /// <example>
    /// <code>
    /// var bucket = storage.Vectors.From("embeddings-prod");
    /// var page = await bucket.ListIndexesAsync(new ListVectorIndexesOptions { Prefix = "docs-" });
    /// while (page.NextToken is not null)
    /// {
    ///     page = await bucket.ListIndexesAsync(new ListVectorIndexesOptions { Prefix = "docs-", NextToken = page.NextToken });
    /// }
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.list_indexes")]
    Task<VectorListPage<VectorIndexSummary>> ListIndexesAsync(ListVectorIndexesOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vector index from this bucket, together with its vectors. Requires a service-role key.
    /// </summary>
    /// <param name="indexName">The name of the index to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="indexName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket or the index does not exist
    /// (<c>Code</c> <c>S3VectorNotFoundException</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// await storage.Vectors.From("embeddings-prod").DeleteIndexAsync("docs-v1");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.delete_index")]
    Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default);
}
