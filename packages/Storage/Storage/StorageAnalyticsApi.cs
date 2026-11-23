using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Attributes;
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
    [ImplementedCapability("storage.analytics.delete_analytics_bucket")]
    public Task DeleteBucketAsync(string name, CancellationToken cancellationToken = default) =>
        Helpers.MakeRequestAsync(this.requestClient, this.options, HttpMethod.Delete, DeleteAnalyticsBucketPath.For(this.icebergUrl, name), null, this.headers(), cancellationToken);
}
