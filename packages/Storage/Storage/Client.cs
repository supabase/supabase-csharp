using System.Collections.Generic;
using Supabase.Core.Attributes;
using Supabase.Storage.AnalyticsBuckets;
using Supabase.Storage.Interfaces;
using Supabase.Storage.VectorBuckets;

namespace Supabase.Storage;

public class Client : StorageBucketApi, IStorageClient<Bucket, FileObject>
{
    public Client(string url, Dictionary<string, string>? headers = null) : base(url, headers)
    { }

    public Client(string url, ClientOptions? options, Dictionary<string, string>? headers = null) : base(url, options, headers)
    { }

    /// <inheritdoc />
    public IStorageAnalyticsApi Analytics =>
        new StorageAnalyticsApi(this.Url, this.Options, this.requestClient, () => this.Headers);

    /// <summary>
    /// Perform a file operation in a bucket
    /// </summary>
    /// <param name="id">Bucket Id</param>
    /// <returns></returns>
    [ImplementedCapability("storage.file_buckets.access_bucket")]
    public IStorageFileApi<FileObject> From(string id) =>
        new StorageFileApi(this.Url, id, this.Options, this.Headers, this.requestClient, this.uploadClient, this.downloadClient);

    /// <inheritdoc />
    public IStorageVectorBucketApi Vectors =>
        new StorageVectorBucketApi(this.Url, this.Options, this.requestClient, () => this.Headers);
}
