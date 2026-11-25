using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.Common;
using Supabase.Storage.ListVectors;

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

    /// <summary>
    /// Retrieves the vectors stored under the given keys in this index. A key that matches no vector is left out of
    /// the result. Requires a service-role key.
    /// </summary>
    /// <param name="keys">The keys of the vectors to retrieve. The service judges the batch size and each key.</param>
    /// <param name="include">
    /// What to return with each key. With <see cref="VectorContent.None"/>, the service applies its own default.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The vectors found, with their data and metadata when <paramref name="include"/> asks for them.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="keys"/> is null.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket or the index does not exist
    /// (<c>Code</c> <c>NotFoundException</c>) or the keys are invalid, such as an empty batch, an empty key
    /// or more keys than the service accepts at once (<c>Code</c> <c>InvalidRequest</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// var vectors = await storage.Vectors.From("embeddings-prod").Index("docs-v1")
    ///     .GetVectorsAsync(new[] { "doc-1" }, VectorContent.Data | VectorContent.Metadata);
    /// var title = vectors[0].Metadata?["title"].GetString();
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.get_vectors")]
    Task<IReadOnlyList<StoredVector>> GetVectorsAsync(IReadOnlyCollection<string> keys, VectorContent include = VectorContent.None, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the vectors of this index, one page at a time. Pass the <see cref="VectorListPage{TItem}.NextToken"/> of a
    /// page back in <see cref="ListVectorsOptions.NextToken"/> to read the next one. Requires a service-role key.
    /// </summary>
    /// <param name="options">Paging, content and segment options. When null, the service applies its defaults.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>One page of vectors, with their data and metadata when <see cref="ListVectorsOptions.Include"/> asks for them.</returns>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket or the index does not exist
    /// (<c>Code</c> <c>NotFoundException</c>), a value is out of range (<c>Code</c> <c>InvalidRequest</c>) or the
    /// segment index is not below the segment count (<c>Code</c> <c>InvalidParameter</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// var index = storage.Vectors.From("embeddings-prod").Index("docs-v1");
    /// var options = new ListVectorsOptions { MaxResults = 100, Include = VectorContent.Metadata };
    /// var page = await index.ListVectorsAsync(options);
    /// while (page.NextToken is not null)
    ///     page = await index.ListVectorsAsync(options with { NextToken = page.NextToken });
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.list_vectors")]
    Task<VectorListPage<StoredVector>> ListVectorsAsync(ListVectorsOptions? options = null, CancellationToken cancellationToken = default);
}
