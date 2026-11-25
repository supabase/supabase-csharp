using System;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.DeleteVectorIndex;
using Supabase.Storage.GetVectorIndex;
using Supabase.Storage.Interfaces;

namespace Supabase.Storage;

internal sealed class VectorBucket : IVectorBucket
{
    private readonly string vectorBucketName;
    private readonly VectorService service;

    internal VectorBucket(string vectorBucketName, VectorService service)
    {
        this.vectorBucketName = vectorBucketName;
        this.service = service;
    }

    /// <inheritdoc />
    public IVectorIndex Index(string indexName)
    {
        VectorService.RequireName(indexName, nameof(indexName));
        return new VectorIndex(this.vectorBucketName, indexName, this.service);
    }

    /// <inheritdoc />
    public async Task<VectorIndexDetails> GetIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        VectorService.RequireName(indexName, nameof(indexName));
        var response = await this.service.PostAsync<GetVectorIndexResponse>("GetIndex", new GetVectorIndexBody(this.vectorBucketName, indexName), cancellationToken).ConfigureAwait(false);
        return response.ToDetails();
    }

    /// <inheritdoc />
    public Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        VectorService.RequireName(indexName, nameof(indexName));
        return this.service.Post("DeleteIndex", new DeleteVectorIndexBody(this.vectorBucketName, indexName), cancellationToken);
    }

    /// <inheritdoc />
    public Task CreateIndexAsync(NewVectorIndex index, CancellationToken cancellationToken = default)
    {
        if (index is null)
            throw new ArgumentNullException(nameof(index));
        VectorService.RequireName(index.IndexName, nameof(index.IndexName));
        return this.service.Post("CreateIndex", new CreateVectorIndexBody(this.vectorBucketName, index), cancellationToken);
    }
}
