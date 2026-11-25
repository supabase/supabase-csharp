using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.DeleteVectorBucket;
using Supabase.Storage.Interfaces;
using Supabase.Storage.ListVectorBuckets;

namespace Supabase.Storage;

internal sealed class StorageVectorBucketApi : IStorageVectorBucketApi
{
    private readonly VectorService service;

    internal StorageVectorBucketApi(string storageUrl, ClientOptions options, HttpClient requestClient, Func<Dictionary<string, string>> headers) =>
        this.service = new VectorService(storageUrl, options, requestClient, headers);

    /// <inheritdoc />
    public IVectorBucket From(string vectorBucketName)
    {
        VectorService.RequireName(vectorBucketName, nameof(vectorBucketName));
        return new VectorBucket(vectorBucketName, this.service);
    }

    /// <inheritdoc />
    public async Task<VectorListPage<VectorBucketSummary>> ListVectorBucketsAsync(ListVectorBucketsOptions? options = null, CancellationToken cancellationToken = default)
    {
        var body = new ListVectorBucketsBody(options ?? new ListVectorBucketsOptions());
        var response = await this.service.PostAsync<ListVectorBucketsResponse>("ListVectorBuckets", body, cancellationToken).ConfigureAwait(false);
        return response.ToPage();
    }

    /// <inheritdoc />
    public Task DeleteVectorBucketAsync(string vectorBucketName, CancellationToken cancellationToken = default)
    {
        VectorService.RequireName(vectorBucketName, nameof(vectorBucketName));
        return this.service.Post("DeleteVectorBucket", new DeleteVectorBucketBody(vectorBucketName), cancellationToken);
    }
}
