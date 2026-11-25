using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.DeleteVectors;
using Supabase.Storage.GetVectors;
using Supabase.Storage.Interfaces;
using Supabase.Storage.ListVectors;
using Supabase.Storage.PutVectors;
using Supabase.Storage.QueryVectors;

namespace Supabase.Storage;

internal sealed class VectorIndex : IVectorIndex
{
    private readonly string vectorBucketName;
    private readonly string indexName;
    private readonly VectorService service;

    internal VectorIndex(string vectorBucketName, string indexName, VectorService service)
    {
        this.vectorBucketName = vectorBucketName;
        this.indexName = indexName;
        this.service = service;
    }

    /// <inheritdoc />
    public Task DeleteVectorsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys is null)
            throw new ArgumentNullException(nameof(keys));
        return this.service.Post("DeleteVectors", new DeleteVectorsBody(this.vectorBucketName, this.indexName, keys), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredVector>> GetVectorsAsync(IReadOnlyCollection<string> keys, VectorContent include = VectorContent.None, CancellationToken cancellationToken = default)
    {
        if (keys is null)
            throw new ArgumentNullException(nameof(keys));
        var response = await this.service.PostAsync<GetVectorsResponse>("GetVectors", new GetVectorsBody(this.vectorBucketName, this.indexName, keys, include), cancellationToken).ConfigureAwait(false);
        return response.ToStoredVectors();
    }

    /// <inheritdoc />
    public async Task<VectorListPage<StoredVector>> ListVectorsAsync(ListVectorsOptions? options = null, CancellationToken cancellationToken = default)
    {
        var body = new ListVectorsBody(this.vectorBucketName, this.indexName, options ?? new ListVectorsOptions());
        var response = await this.service.PostAsync<ListVectorsResponse>("ListVectors", body, cancellationToken).ConfigureAwait(false);
        return response.ToPage();
    }

    /// <inheritdoc />
    public Task PutVectorsAsync(IReadOnlyCollection<NewVector> vectors, CancellationToken cancellationToken = default)
    {
        if (vectors is null)
            throw new ArgumentNullException(nameof(vectors));
        return this.service.Post("PutVectors", new PutVectorsBody(this.vectorBucketName, this.indexName, vectors), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VectorQueryResult> QueryVectorsAsync(IReadOnlyList<float> queryVector, int topK, QueryVectorsOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (queryVector is null)
            throw new ArgumentNullException(nameof(queryVector));
        var body = new QueryVectorsBody(this.vectorBucketName, this.indexName, queryVector, topK, options ?? new QueryVectorsOptions());
        var response = await this.service.PostAsync<QueryVectorsResponse>("QueryVectors", body, cancellationToken).ConfigureAwait(false);
        return response.ToResult();
    }
}
