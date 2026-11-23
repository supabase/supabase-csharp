using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;

namespace Supabase.Storage.Interfaces;

/// <summary>
/// Operations scoped to one vector bucket, obtained from <see cref="IStorageVectorBucketApi.From"/>.
/// </summary>
public interface IVectorBucket
{
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
