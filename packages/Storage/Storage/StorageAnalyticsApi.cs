using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.Common;
using Supabase.Storage.CreateAnalyticsBucket;
using Supabase.Storage.DeleteAnalyticsBucket;
using Supabase.Storage.Interfaces;
using Supabase.Storage.ListAnalyticsBuckets;

namespace Supabase.Storage;

internal class StorageAnalyticsApi : IStorageAnalyticsApi
{
    private readonly string icebergUrl;
    private readonly ClientOptions options;
    private readonly HttpClient requestClient;
    private readonly Func<Dictionary<string, string>> headers;

    internal StorageAnalyticsApi(string storageUrl, ClientOptions options, HttpClient requestClient, Func<Dictionary<string, string>> headers)
    {
        this.icebergUrl = $"{storageUrl}/iceberg";
        this.options = options;
        this.requestClient = requestClient;
        this.headers = headers;
    }

    /// <inheritdoc />
    public IIcebergCatalog From(string bucketName) =>
        new IcebergCatalog($"{this.icebergUrl}/v1/{AnalyticsBucketName.Escape(bucketName, nameof(bucketName))}", this.options, this.requestClient, this.headers);

    /// <inheritdoc />
    public async Task<AnalyticsBucket> CreateBucketAsync(string name, CancellationToken cancellationToken = default)
    {
        var body = CreateAnalyticsBucketBody.For(name);
        var bucket = await Helpers.MakeRequestAsync<AnalyticsBucket>(this.requestClient, this.options, HttpMethod.Post, $"{this.icebergUrl}/bucket", body, this.headers(), cancellationToken).ConfigureAwait(false);
        return bucket!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnalyticsBucket>> ListBucketsAsync(ListAnalyticsBucketsOptions? options = null, CancellationToken cancellationToken = default)
    {
        var url = ListAnalyticsBucketsQuery.For($"{this.icebergUrl}/bucket", options);
        var buckets = await Helpers.MakeRequestAsync<List<AnalyticsBucket>>(this.requestClient, this.options, HttpMethod.Get, url, null, this.headers(), cancellationToken).ConfigureAwait(false);
        return buckets ?? new List<AnalyticsBucket>();
    }

    /// <inheritdoc />
    public Task DeleteBucketAsync(string name, CancellationToken cancellationToken = default) =>
        Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Delete, DeleteAnalyticsBucketPath.For(this.icebergUrl, name), null, this.headers(), cancellationToken);
}
