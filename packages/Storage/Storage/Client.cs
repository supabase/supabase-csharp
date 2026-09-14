using System.Collections.Generic;
using Supabase.Storage.Interfaces;

namespace Supabase.Storage;

public class Client : StorageBucketApi, IStorageClient<Bucket, FileObject>
{
    public Client(string url, Dictionary<string, string>? headers = null) : base(url, headers)
    { }

    public Client(string url, ClientOptions? options, Dictionary<string, string>? headers = null) : base(url, options, headers)
    { }

    /// <summary>
    /// Perform a file operation in a bucket
    /// </summary>
    /// <param name="id">Bucket Id</param>
    /// <returns></returns>
    public IStorageFileApi<FileObject> From(string id) => new StorageFileApi(this.Url, id, this.Options, this.Headers);
}
