using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Storage.CreateAnalyticsBucket;
using Supabase.Storage.DeleteAnalyticsBucket;
using Supabase.Storage.Interfaces;

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
    public async Task<AnalyticsBucket> CreateBucketAsync(string name, CancellationToken cancellationToken = default)
    {
        var body = CreateAnalyticsBucketBody.For(name);
        var bucket = await Helpers.MakeRequestAsync<AnalyticsBucket>(this.requestClient, this.options, HttpMethod.Post, $"{this.icebergUrl}/bucket", body, this.headers(), cancellationToken).ConfigureAwait(false);
        return bucket!;
    }

    /// <inheritdoc />
    public Task DeleteBucketAsync(string name, CancellationToken cancellationToken = default) =>
        Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Delete, DeleteAnalyticsBucketPath.For(this.icebergUrl, name), null, this.headers(), cancellationToken);
}
