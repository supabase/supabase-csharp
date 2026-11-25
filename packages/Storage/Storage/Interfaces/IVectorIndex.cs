using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;

namespace Supabase.Storage.Interfaces;

/// <summary>
/// Operations scoped to one vector index of one vector bucket, obtained from <see cref="IVectorBucket.Index"/>.
/// </summary>
public interface IVectorIndex
{
    /// <summary>
    /// Deletes the vectors stored under the given keys from this index. A key that matches no vector is
    /// ignored. Requires a service-role key.
    /// </summary>
    /// <param name="keys">The keys of the vectors to delete. The service judges the batch size and each key.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="keys"/> is null.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket or the index does not exist
    /// (<c>Code</c> <c>NotFoundException</c>) or the keys are invalid, such as an empty batch, an empty key
    /// or more keys than the service accepts at once (<c>Code</c> <c>InvalidRequest</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// await storage.Vectors.From("embeddings-prod").Index("docs-v1")
    ///     .DeleteVectorsAsync(new[] { "doc-1", "doc-2" });
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.delete_vectors")]
    Task DeleteVectorsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}
