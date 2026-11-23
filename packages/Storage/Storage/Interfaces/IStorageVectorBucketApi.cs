using System.Threading;
using System.Threading.Tasks;

namespace Supabase.Storage.Interfaces;

/// <summary>
/// Operations on vector buckets, which hold the vector indexes of the Storage vector service.
/// </summary>
public interface IStorageVectorBucketApi
{
    /// <summary>
    /// Deletes an empty vector bucket. The service rejects the call while the bucket still has
    /// indexes. Requires a service-role key.
    /// </summary>
    /// <param name="vectorBucketName">The name of the vector bucket to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="vectorBucketName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket does not exist
    /// (<c>Code</c> <c>S3VectorNotFoundException</c>) or is not empty (<c>Code</c> <c>S3VectorBucketNotEmpty</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// await storage.Vectors.DeleteVectorBucketAsync("embeddings-prod");
    /// </code>
    /// </example>
    Task DeleteVectorBucketAsync(string vectorBucketName, CancellationToken cancellationToken = default);
}
