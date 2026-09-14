using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core;
using Supabase.Core.Extensions;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Extensions;
using Supabase.Storage.Interfaces;

namespace Supabase.Storage;

public class StorageBucketApi : IStorageBucketApi<Bucket>
{
    public ClientOptions Options { get; protected set; }
    protected string Url { get; set; }

    private readonly HttpClient requestClient;
    private readonly HttpClient uploadClient;
    private readonly HttpClient downloadClient;

    private readonly Dictionary<string, string> initializedHeaders;
    private Dictionary<string, string> headers;
    public Dictionary<string, string> Headers
    {
        get
        {
            if (this.GetHeaders != null)
                this.headers = this.GetHeaders();

            return this.headers.MergeLeft(this.initializedHeaders);
        }
        set
        {
            this.headers = value;

            if (!this.headers.ContainsKey("X-Client-Info"))
                this.headers.Add("X-Client-Info", Util.GetAssemblyVersion(typeof(Client)));
        }
    }

    /// <summary>
    /// Function that can be set to return dynamic headers.
    /// 
    /// Headers specified in the constructor will ALWAYS take precendece over headers returned by this function.
    /// </summary>
    public Func<Dictionary<string, string>>? GetHeaders { get; set; }

    // Resolves the HttpClients only after Options has settled to its final, caller-supplied value — a
    // previous version resolved these before a chained-from ctor's `this.Options = options` ran, so a
    // custom ClientOptions' timeouts/injected clients were silently ignored in favor of a throwaway
    // default ClientOptions().
    protected StorageBucketApi(string url, ClientOptions? options, Dictionary<string, string>? headers = null)
    {
        this.Url = url;
        this.Options = options ?? new ClientOptions();

        this.requestClient = Helpers.ResolveRequestClient(this.Options);
        this.uploadClient = Helpers.ResolveUploadClient(this.Options);
        this.downloadClient = Helpers.ResolveDownloadClient(this.Options);

        headers ??= new Dictionary<string, string>();
        this.headers = headers;
        this.initializedHeaders = headers;
    }

    protected StorageBucketApi(string url, Dictionary<string, string>? headers = null)
        : this(url, (ClientOptions?) null, headers)
    { }

    /// <summary>
    /// Retrieves the details of all Storage buckets within an existing product.
    /// </summary>
    /// <returns></returns>
    public Task<List<Bucket>?> ListBuckets() =>
         Helpers.MakeRequestAsync<List<Bucket>>(this.requestClient, this.Options.Retry, HttpMethod.Get, $"{this.Url}/bucket", null, this.Headers);

    /// <summary>
    /// Retrieves the details of an existing Storage bucket.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<Bucket?> GetBucket(string id)
    {
        try
        {
            var result = await Helpers.MakeRequestAsync<Bucket>(this.requestClient, this.Options.Retry, HttpMethod.Get, $"{this.Url}/bucket/{id}", null, this.Headers);
            return result;
        }
        catch (SupabaseStorageException ex)
        {
            if (ex.Reason == FailureHint.Reason.NotFound)
                return null;
            else
                throw;
        }
    }

    /// <summary>
    /// Creates a new Storage bucket
    /// </summary>
    /// <param name="id"></param>
    /// <param name="options"></param>
    /// <returns>Bucket Id</returns>
    public async Task<string> CreateBucket(string id, BucketUpsertOptions? options = null)
    {
        options ??= new BucketUpsertOptions();

        var data = new Bucket
        {
            Id = id,
            Name = id,
            Public = options.Public,
            FileSizeLimit = options.FileSizeLimit,
            AllowedMimes = options.AllowedMimes
        };

        var result = await Helpers.MakeRequestAsync<Bucket>(this.requestClient, this.Options.Retry, HttpMethod.Post, $"{this.Url}/bucket", data, this.Headers);

        return result?.Name!;
    }

    /// <summary>
    /// Updates a Storage bucket
    /// </summary>
    /// <param name="id"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task<Bucket?> UpdateBucket(string id, BucketUpsertOptions? options = null)
    {
        options ??= new BucketUpsertOptions();

        var data = new Bucket
        {
            Id = id,
            Public = options.Public,
            FileSizeLimit = options.FileSizeLimit,
            AllowedMimes = options.AllowedMimes
        };

        var result = await Helpers.MakeRequestAsync<Bucket>(this.requestClient, this.Options.Retry, HttpMethod.Put, $"{this.Url}/bucket/{id}", data, this.Headers);

        return result;
    }

    /// <summary>
    /// Removes all objects inside a single bucket.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task<GenericResponse?> EmptyBucket(string id) =>
        Helpers.MakeRequestAsync<GenericResponse>(this.requestClient, this.Options.Retry, HttpMethod.Post, $"{this.Url}/bucket/{id}/empty", null, this.Headers);

    /// <summary>
    /// Deletes an existing bucket. A bucket can't be deleted with existing objects inside it.
    /// You must first <see cref="EmptyBucket(string)"/>
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task<GenericResponse?> DeleteBucket(string id) =>
        Helpers.MakeRequestAsync<GenericResponse>(this.requestClient, this.Options.Retry, HttpMethod.Delete, $"{this.Url}/bucket/{id}", null, this.Headers);

    /// <inheritdoc />
    public Task<GenericResponse?> PurgeBucketCache(
        string id,
        PurgeCacheOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = options.ToPurgeUrl($"{this.Url}/cdn/{Helpers.EncodePath(id)}");
        return Helpers.MakeRequestAsync<GenericResponse>(this.requestClient, this.Options.Retry, HttpMethod.Delete, url, null, this.Headers, cancellationToken);
    }
}
