using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
using Supabase.Storage.Common;
using Supabase.Storage.VectorBuckets.Common;
using Supabase.Storage.VectorBuckets.ListVectors;
using Supabase.Storage.VectorBuckets.PutVectors;
using Supabase.Storage.VectorBuckets.QueryVectors;

namespace Supabase.Storage.VectorBuckets;

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
    /// Lists the vectors of this index, one page at a time. Pass the <see cref="Page{T}.NextPageToken"/> of a
    /// page back in <see cref="ListVectorsOptions.NextPageToken"/> to read the next one. Requires a service-role key.
    /// To scan a large index in parallel, give each worker its own <see cref="ListVectorsOptions.Segment"/> of the same
    /// count: every vector belongs to exactly one segment, and each worker pages through its segment alone.
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
    /// var options = new ListVectorsOptions { PageSize = 100, Include = VectorContent.Metadata };
    /// Page&lt;StoredVector&gt; page;
    /// do
    /// {
    ///     page = await index.ListVectorsAsync(options);
    ///     foreach (var vector in page.Items)
    ///         Console.WriteLine(vector.Key);
    ///     options = options with { NextPageToken = page.NextPageToken };
    /// }
    /// while (page.NextPageToken is not null);
    /// </code>
    /// A full scan split across four parallel workers:
    /// <code>
    /// const int segmentCount = 4;
    /// var index = storage.Vectors.From("embeddings-prod").Index("docs-v1");
    /// var scans = Enumerable.Range(0, segmentCount).Select(async segmentIndex =>
    /// {
    ///     var keys = new List&lt;string&gt;();
    ///     var options = new ListVectorsOptions { Segment = new VectorSegment(segmentIndex, segmentCount) };
    ///     Page&lt;StoredVector&gt; page;
    ///     do
    ///     {
    ///         page = await index.ListVectorsAsync(options);
    ///         keys.AddRange(page.Items.Select(vector => vector.Key));
    ///         options = options with { NextPageToken = page.NextPageToken };
    ///     }
    ///     while (page.NextPageToken is not null);
    ///     return keys;
    /// });
    /// var allKeys = (await Task.WhenAll(scans)).SelectMany(keys => keys);
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.list_vectors")]
    [ImplementedCapability("storage.vector_buckets.parallel_scan")]
    Task<Page<StoredVector>> ListVectorsAsync(ListVectorsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the vectors in this index. A vector whose key is already in use replaces the stored one. Requires a
    /// service-role key.
    /// </summary>
    /// <param name="vectors">
    /// The vectors to store. The service judges the batch size, each key, the component count and the metadata.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="vectors"/> is null.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket or the index does not exist
    /// (<c>Code</c> <c>NotFoundException</c>) or the batch is invalid, such as an empty batch, an empty or duplicated
    /// key or more vectors than the service accepts at once (<c>Code</c> <c>InvalidRequest</c> or <c>InvalidParameter</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// await storage.Vectors.From("embeddings-prod").Index("docs-v1").PutVectorsAsync(new[]
    /// {
    ///     new NewVector
    ///     {
    ///         Key = "doc-1",
    ///         Data = new[] { 0.5f, 0.25f },
    ///         Metadata = new Dictionary&lt;string, JsonElement&gt; { ["title"] = JsonSerializer.SerializeToElement("Intro") },
    ///     },
    /// });
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.put_vectors")]
    Task PutVectorsAsync(IReadOnlyCollection<NewVector> vectors, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches this index for the vectors closest to the query vector, nearest first. Requires a service-role key.
    /// </summary>
    /// <param name="queryVector">The 32-bit floating point components to search around. The service checks the count against the index dimension.</param>
    /// <param name="topK">The number of matches to return. The service accepts 1 to 10,000 on S3 Vectors and a lower limit on other backends.</param>
    /// <param name="options">Filter, content and paging options. When null, the service applies its defaults.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The matches nearest first, with their distance and metadata when <see cref="QueryVectorsOptions.Include"/> asks for them.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="queryVector"/> is null.</exception>
    /// <exception cref="Exceptions.SupabaseStorageException">
    /// The service refused the call, for example because the bucket or the index does not exist
    /// (<c>Code</c> <c>NotFoundException</c>) or a value is invalid, such as a <paramref name="topK"/> out of range or a
    /// malformed filter (<c>Code</c> <c>InvalidRequest</c> or <c>InvalidParameter</c>).
    /// </exception>
    /// <example>
    /// <code>
    /// var index = storage.Vectors.From("embeddings-prod").Index("docs-v1");
    /// var options = new QueryVectorsOptions
    /// {
    ///     Include = VectorMatchContent.Distance | VectorMatchContent.Metadata,
    ///     Filter = JsonDocument.Parse("{\"category\":\"docs\"}").RootElement,
    /// };
    /// var result = await index.QueryVectorsAsync(new[] { 0.5f, 0.25f }, topK: 5, options);
    /// foreach (var match in result.Matches)
    ///     Console.WriteLine($"{match.Key} {match.Distance}");
    /// </code>
    /// </example>
    [ImplementedCapability("storage.vector_buckets.query_vectors")]
    Task<VectorQueryResult> QueryVectorsAsync(IReadOnlyList<float> queryVector, int topK, QueryVectorsOptions? options = null, CancellationToken cancellationToken = default);
}
