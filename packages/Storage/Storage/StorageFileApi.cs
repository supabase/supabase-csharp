using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BirdMessenger.Collections;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Extensions;
using Supabase.Storage.Interfaces;
using Supabase.Storage.Responses;

namespace Supabase.Storage;

/// <inheritdoc />
public class StorageFileApi : IStorageFileApi<FileObject>
{
    /// <summary>
    /// Serializes <see cref="TransformOptions"/> with its <see cref="TransformOptions.ResizeType"/>
    /// enum rendered as its lowercase wire token (e.g. <c>cover</c>) rather than a number — matching
    /// the previous Newtonsoft.Json <c>StringEnumConverter</c>. The enum member names map one-to-one
    /// to their camel-cased wire values, so the built-in converter reproduces the exact output the
    /// approval snapshots pin. Relaxed escaping mirrors the package-wide options.
    /// </summary>
    private static readonly JsonSerializerOptions TransformSerializerOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <inheritdoc />
    public ClientOptions Options { get; protected set; }
    /// <summary>
    ///
    /// </summary>
    protected string Url { get; set; }
    /// <summary>
    ///
    /// </summary>
    protected Dictionary<string, string> Headers { get; set; }
    /// <summary>
    ///
    /// </summary>
    protected string? BucketId { get; set; }

    /// <summary>
    ///
    /// </summary>
    protected readonly Header StorageHeader = new();

    private readonly HttpClient requestClient;
    private readonly HttpClient uploadClient;
    private readonly HttpClient downloadClient;

    /// <summary>
    ///
    /// </summary>
    /// <param name="url"></param>
    /// <param name="bucketId"></param>
    /// <param name="options"></param>
    /// <param name="headers"></param>
    // Resolves the HttpClients only after Options has settled to its final, caller-supplied value — a
    // previous version resolved these before a chained-from ctor's `this.Options = options` ran, so a
    // custom ClientOptions' timeouts/injected clients were silently ignored in favor of a throwaway
    // default ClientOptions().
    public StorageFileApi(
        string url,
        string bucketId,
        ClientOptions? options,
        Dictionary<string, string>? headers = null
    )
    {
        this.Url = url;
        this.BucketId = bucketId;
        this.Options = options ?? new ClientOptions();
        this.Headers = headers ?? new Dictionary<string, string>();
        this.StorageHeader.Add(this.Headers);

        this.requestClient = Helpers.ResolveRequestClient(this.Options);
        this.uploadClient = Helpers.ResolveUploadClient(this.Options);
        this.downloadClient = Helpers.ResolveDownloadClient(this.Options);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="url"></param>
    /// <param name="headers"></param>
    /// <param name="bucketId"></param>
    public StorageFileApi(
        string url,
        Dictionary<string, string>? headers = null,
        string? bucketId = null
    )
    {
        this.Url = url;
        this.BucketId = bucketId;
        this.Options = new ClientOptions();
        this.Headers = headers ?? new Dictionary<string, string>();
        this.StorageHeader.Add(this.Headers);

        this.requestClient = Helpers.ResolveRequestClient(this.Options);
        this.uploadClient = Helpers.ResolveUploadClient(this.Options);
        this.downloadClient = Helpers.ResolveDownloadClient(this.Options);
    }

    /// <summary>
    /// A simple convenience function to get the URL for an asset in a public bucket. If you do not want to use this function, you can construct the public URL by concatenating the bucket URL with the path to the asset.
    /// This function does not verify if the bucket is public. If a public URL is created for a bucket which is not public, you will not be able to download the asset.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="transformOptions"></param>
    /// <param name="downloadOptions"></param>
    /// <returns></returns>
    public string GetPublicUrl(
        string path,
        TransformOptions? transformOptions,
        DownloadOptions? downloadOptions = null
    )
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);

        if (downloadOptions != null)
            queryParams.Add(downloadOptions.ToQueryCollection());

        if (transformOptions is null or { IsEmpty: true })
        {
            var queryParamsString = queryParams.ToString();
            return $"{this.Url}/object/public/{this.GetFinalPath(path)}?{queryParamsString}";
        }

        queryParams.Add(transformOptions.ToQueryCollection());
        var builder = new UriBuilder($"{this.Url}/render/image/public/{this.GetFinalPath(path)}")
        {
            Query = queryParams.ToString(),
        };

        return builder.ToString();
    }

    /// <summary>
    /// Create signed url to download a file without requiring permissions. This URL can be valid for a set number of seconds.
    /// </summary>
    /// <param name="path">The file path to be downloaded, including the current file name. For example, `folder/image.png`.</param>
    /// <param name="expiresIn">The number of seconds until the signed URL expires. For example, `60` for a URL which is valid for one minute.</param>
    /// <param name="transformOptions"></param>
    /// <param name="downloadOptions"></param>
    /// <returns></returns>
    public async Task<string> CreateSignedUrl(
        string path,
        int expiresIn,
        TransformOptions? transformOptions = null,
        DownloadOptions? downloadOptions = null
    )
    {
        var body = new Dictionary<string, object?> { { "expiresIn", expiresIn } };
        var url = $"{this.Url}/object/sign/{this.GetFinalPath(path)}";

        if (transformOptions is { IsEmpty: false })
        {
            var transformOptionsJson = JsonSerializer.Serialize(
                transformOptions,
                TransformSerializerOptions
            );
            var transformOptionsObj = JsonSerializer.Deserialize<Dictionary<string, object>>(
                transformOptionsJson,
                Helpers.SerializerOptions
            );
            body.Add("transform", transformOptionsObj);
        }

        var response = await Helpers.MakeRequestAsync<CreateSignedUrlResponse>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Post,
            url,
            body,
            this.Headers
        );

        if (response == null || string.IsNullOrEmpty(response.SignedUrl))
            throw new SupabaseStorageException(
                $"Signed Url for {path} returned empty, do you have permission?"
            );

        var downloadQueryParams = downloadOptions?.ToQueryCollection().ToString();
        var downloadSeparator = string.IsNullOrEmpty(downloadQueryParams) ? "" : "&";
        return $"{this.Url}{response.SignedUrl}{downloadSeparator}{downloadQueryParams}";
    }

    /// <summary>
    /// Create signed URLs to download files without requiring permissions. These URLs can be valid for a set number of seconds.
    /// </summary>
    /// <param name="paths">The file paths to be downloaded, including the current file names. For example [`folder/image.png`, 'folder2/image2.png'].</param>
    /// <param name="expiresIn">The number of seconds until the signed URLs expire. For example, `60` for URLs which are valid for one minute.</param>
    /// <param name="downloadOptions"></param>
    /// <returns></returns>
    public async Task<List<CreateSignedUrlsResponse>?> CreateSignedUrls(
        List<string> paths,
        int expiresIn,
        DownloadOptions? downloadOptions = null
    )
    {
        var body = new Dictionary<string, object>
        {
            { "expiresIn", expiresIn },
            { "paths", paths },
        };
        var response = await Helpers.MakeRequestAsync<List<CreateSignedUrlsResponse>>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Post,
            $"{this.Url}/object/sign/{this.BucketId}",
            body,
            this.Headers
        );

        var downloadQueryParams = downloadOptions?.ToQueryCollection().ToString();
        var downloadSeparator = string.IsNullOrEmpty(downloadQueryParams) ? "" : "&";
        if (response != null)
        {
            foreach (var item in response)
            {
                if (string.IsNullOrEmpty(item.SignedUrl))
                    throw new SupabaseStorageException(
                        $"Signed Url for {item.Path} returned empty, do you have permission?"
                    );

                item.SignedUrl = $"{this.Url}{item.SignedUrl}{downloadSeparator}{downloadQueryParams}";
            }
        }

        return response;
    }

    /// <summary>
    /// Lists all the files within a bucket.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task<List<FileObject>?> List(string path = "", SearchOptions? options = null)
    {
        options ??= new SearchOptions();

        var json = JsonSerializer.Serialize(options, Helpers.SerializerOptions);
        var body = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Helpers.SerializerOptions);

        if (body != null)
            body.Add("prefix", string.IsNullOrEmpty(path) ? "" : path);

        var response = await Helpers.MakeRequestAsync<List<FileObject>>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Post,
            $"{this.Url}/object/list/{this.BucketId}",
            body,
            this.Headers
        );

        return response;
    }

    /// <summary>
    /// Retrieves the details of an existing file.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public async Task<FileObjectV2?> Info(string path)
    {
        var response = await Helpers.MakeRequestAsync<FileObjectV2>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Get,
            $"{this.Url}/object/info/{this.BucketId}/{path}",
            null,
            this.Headers
        );

        return response;
    }

    /// <summary>
    /// Uploads a file to an existing bucket.
    /// </summary>
    /// <remarks>
    /// This is a single-request upload and is subject to the gateway's request-size limit; a file
    /// past that limit fails with a <see cref="SupabaseStorageException"/> of
    /// <see cref="FailureHint.Reason.EntityTooLarge"/>. For large files use the resumable
    /// <see cref="UploadOrResume(string, string, FileOptions?, EventHandler{float}?, CancellationToken)"/>.
    /// </remarks>
    /// <param name="localFilePath">File Source Path</param>
    /// <param name="supabasePath">The relative file path. Should be of the format `folder/subfolder/filename.png`. The bucket must already exist before attempting to upload.</param>
    /// <param name="options"></param>
    /// <param name="onProgress"></param>
    /// <param name="inferContentType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> Upload(
        string localFilePath,
        string supabasePath,
        FileOptions? options = null,
        EventHandler<float>? onProgress = null,
        bool inferContentType = true,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new FileOptions();

        if (inferContentType)
            options.ContentType = MimeMapping.MimeUtility.GetMimeMapping(localFilePath);

        var result = await this.UploadOrUpdate(localFilePath, supabasePath, options, onProgress, cancellationToken);
        return result;
    }

    /// <summary>
    /// Uploads a byte array to an existing bucket.
    /// </summary>
    /// <remarks>
    /// This is a single-request upload and is subject to the gateway's request-size limit; data
    /// past that limit fails with a <see cref="SupabaseStorageException"/> of
    /// <see cref="FailureHint.Reason.EntityTooLarge"/>. For large payloads use the resumable
    /// <see cref="UploadOrResume(byte[], string, FileOptions?, EventHandler{float}?, CancellationToken)"/>.
    /// </remarks>
    /// <param name="data"></param>
    /// <param name="supabasePath">The relative file path. Should be of the format `folder/subfolder/filename.png`. The bucket must already exist before attempting to upload.</param>
    /// <param name="options"></param>
    /// <param name="onProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<string> Upload(
        byte[] data,
        string supabasePath,
        FileOptions? options = null,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default
    ) => this.Upload(data, supabasePath, options, onProgress, true, cancellationToken);

    /// <summary>
    /// Uploads a byte array to an existing bucket.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="supabasePath">The relative file path. Should be of the format `folder/subfolder/filename.png`. The bucket must already exist before attempting to upload.</param>
    /// <param name="options"></param>
    /// <param name="onProgress"></param>
    /// <param name="inferContentType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> Upload(
        byte[] data,
        string supabasePath,
        FileOptions? options = null,
        EventHandler<float>? onProgress = null,
        bool inferContentType = true,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new FileOptions();

        if (inferContentType)
            options.ContentType = MimeMapping.MimeUtility.GetMimeMapping(supabasePath);

        var result = await this.UploadOrUpdate(data, supabasePath, options, onProgress, cancellationToken);
        return result;
    }

    /// <summary>
    /// Uploads a file to using a pre-generated Signed Upload Url
    /// </summary>
    /// <param name="localFilePath">File Source Path</param>
    /// <param name="signedUrl"></param>
    /// <param name="options"></param>
    /// <param name="onProgress"></param>
    /// <param name="inferContentType"></param>
    /// <returns></returns>
    public async Task<string> UploadToSignedUrl(
        string localFilePath,
        UploadSignedUrl signedUrl,
        FileOptions? options = null,
        EventHandler<float>? onProgress = null,
        bool inferContentType = true
    )
    {
        options ??= new FileOptions();

        if (inferContentType)
            options.ContentType = MimeMapping.MimeUtility.GetMimeMapping(localFilePath);

        this.StorageHeader.Add("Authorization", $"Bearer {signedUrl.Token}");
        this.StorageHeader.Add("cache-control", $"max-age={options.CacheControl}");
        this.StorageHeader.Add("content-type", options.ContentType);

        if (options.Upsert)
            this.StorageHeader.Add("x-upsert", options.Upsert.ToString().ToLower());

        if (options.Metadata != null)
            this.StorageHeader.Add("x-metadata", ParseMetadata(options.Metadata));

        options.Headers?.ToList().ForEach(x => this.StorageHeader.Add(x.Key, x.Value));

        var progress = new Progress<float>();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        await this.uploadClient.UploadFileAsync(
            signedUrl.SignedUrl,
            localFilePath,
            this.StorageHeader.Get(),
            progress
        );

        return this.GetFinalPath(signedUrl.Key);
    }

    /// <summary>
    /// Uploads a byte array using a pre-generated Signed Upload Url
    /// </summary>
    /// <param name="data"></param>
    /// <param name="signedUrl"></param>
    /// <param name="options"></param>
    /// <param name="onProgress"></param>
    /// <param name="inferContentType"></param>
    /// <returns></returns>
    public async Task<string> UploadToSignedUrl(
        byte[] data,
        UploadSignedUrl signedUrl,
        FileOptions? options = null,
        EventHandler<float>? onProgress = null,
        bool inferContentType = true
    )
    {
        options ??= new FileOptions();

        if (inferContentType)
            options.ContentType = MimeMapping.MimeUtility.GetMimeMapping(signedUrl.Key);

        this.StorageHeader.Add("Authorization", $"Bearer {signedUrl.Token}");
        this.StorageHeader.Add("cache-control", $"max-age={options.CacheControl}");
        this.StorageHeader.Add("content-type", options.ContentType);

        if (options.Upsert)
            this.StorageHeader.Add("x-upsert", options.Upsert.ToString().ToLower());

        if (options.Metadata != null)
            this.StorageHeader.Add("x-metadata", ParseMetadata(options.Metadata));

        options.Headers?.ToList().ForEach(x => this.StorageHeader.Add(x.Key, x.Value));

        var progress = new Progress<float>();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        await this.uploadClient.UploadBytesAsync(
            signedUrl.SignedUrl,
            data,
            this.StorageHeader.Get(),
            progress
        );

        return this.GetFinalPath(signedUrl.Key);
    }

    /// <summary>
    /// Replaces an existing file at the specified path with a new one.
    /// </summary>
    /// <param name="localFilePath">File source path.</param>
    /// <param name="supabasePath">The relative file path. Should be of the format `folder/subfolder/filename.png`. The bucket must already exist before attempting to upload.</param>
    /// <param name="options">HTTP headers.</param>
    /// <param name="onProgress"></param>
    /// <returns></returns>
    public Task<string> Update(
        string localFilePath,
        string supabasePath,
        FileOptions? options = null,
        EventHandler<float>? onProgress = null
    )
    {
        options ??= new FileOptions();
        return this.UploadOrUpdate(localFilePath, supabasePath, options, onProgress);
    }

    /// <summary>
    /// Replaces an existing file at the specified path with a new one.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="supabasePath">The relative file path. Should be of the format `folder/subfolder/filename.png`. The bucket must already exist before attempting to upload.</param>
    /// <param name="options">HTTP headers.</param>
    /// <param name="onProgress"></param>
    /// <returns></returns>
    public Task<string> Update(
        byte[] data,
        string supabasePath,
        FileOptions? options = null,
        EventHandler<float>? onProgress = null
    )
    {
        options ??= new FileOptions();
        return this.UploadOrUpdate(data, supabasePath, options, onProgress);
    }

    /// <summary>
    /// Attempts to upload a file to Supabase storage. If the upload process is interrupted or incomplete, it will attempt to resume the upload.
    /// </summary>
    /// <param name="localPath">The local file path of the file to be uploaded.</param>
    /// <param name="fileName">The destination path in Supabase Storage where the file will be stored.</param>
    /// <param name="options">Optional file options to specify metadata or other upload configurations.</param>
    /// <param name="onProgress">An optional event handler for tracking and reporting upload progress as a percentage.</param>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>Returns a task that resolves to a string representing the URL or path of the uploaded file in the storage.</returns>
    public Task UploadOrResume(
        string localPath,
        string fileName,
        FileOptions? options,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new FileOptions();
        return this.UploadOrContinue(
            localPath,
            fileName,
            options,
            onProgress,
            cancellationToken
        );
    }

    /// <summary>
    /// Uploads a file to the specified path in Supabase storage or resumes an interrupted upload process.
    /// Allows customization through provided file options and supports tracking upload progress via an event handler.
    /// </summary>
    /// <param name="data">The byte array containing the file data to upload.</param>
    /// <param name="fileName">The destination path within Supabase storage where the file should be stored.</param>
    /// <param name="options">Optional configuration settings for the upload process.</param>
    /// <param name="onProgress">An optional event handler for monitoring the upload progress, reporting it as a percentage.</param>
    /// <param name="cancellationToken">A cancellation token to observe while awaiting the task, allowing the operation to be canceled.</param>
    /// <returns>A task representing the asynchronous operation, resolving to the path of the uploaded file upon successful completion.</returns>
    public Task UploadOrResume(
        byte[] data,
        string fileName,
        FileOptions? options,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new FileOptions();
        return this.UploadOrContinue(data, fileName, options, onProgress, cancellationToken);
    }

    /// <summary>
    /// Moves an existing file to a new location, optionally allowing renaming.
    /// </summary>
    /// <param name="fromPath">The original file path, including the current file name (e.g., `folder/image.png`).</param>
    /// <param name="toPath">The target file path, including the new file name (e.g., `folder/image-copy.png`).</param>
    /// <param name="options">Optional parameters for specifying the destination bucket and other settings.</param>
    /// <returns>Returns a boolean value indicating whether the operation was successful.</returns>
    public async Task<bool> Move(
        string fromPath,
        string toPath,
        DestinationOptions? options = null
    )
    {
        var body = new Dictionary<string, string?>
        {
            { "bucketId", this.BucketId },
            { "sourceKey", fromPath },
            { "destinationKey", toPath },
            { "destinationBucket", options?.DestinationBucket },
        };
        await Helpers.MakeRequestAsync<GenericResponse>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Post,
            $"{this.Url}/object/move",
            body,
            this.Headers
        );
        return true;
    }

    /// <summary>
    /// Copies a file/object from one path to another within a bucket or across buckets.
    /// </summary>
    /// <param name="fromPath">The source path of the file/object to copy.</param>
    /// <param name="toPath">The destination path for the copied file/object.</param>
    /// <param name="options">Optional parameters such as the destination bucket.</param>
    /// <returns>True if the copy operation was successful.</returns>
    public async Task<bool> Copy(
        string fromPath,
        string toPath,
        DestinationOptions? options = null
    )
    {
        var body = new Dictionary<string, string?>
        {
            { "bucketId", this.BucketId },
            { "sourceKey", fromPath },
            { "destinationKey", toPath },
            { "destinationBucket", options?.DestinationBucket },
        };

        await Helpers.MakeRequestAsync<GenericResponse>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Post,
            $"{this.Url}/object/copy",
            body,
            this.Headers
        );
        return true;
    }

    /// <summary>
    /// Downloads a file from a private bucket. For public buckets, use <see>
    ///     <cref>DownloadPublicFile(string, string, TransformOptions?, EventHandler{float}?)</cref>
    /// </see>
    /// </summary>
    /// <param name="supabasePath"></param>
    /// <param name="localPath"></param>
    /// <param name="transformOptions"></param>
    /// <param name="onProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="cacheNonce"></param>
    /// <returns></returns>
    public Task<string> Download(
        string supabasePath,
        string localPath,
        TransformOptions? transformOptions = null,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? cacheNonce = null
    )
    {
        var url =
            transformOptions is { IsEmpty: false }
                ? $"{this.Url}/render/image/authenticated/{this.GetFinalPath(supabasePath)}"
                : $"{this.Url}/object/{this.GetFinalPath(supabasePath)}";
        return this.DownloadFile(url, localPath, transformOptions, onProgress, cancellationToken, cacheNonce);
    }

    /// <summary>
    /// Downloads a file from a private bucket. For public buckets, use <see>
    ///     <cref>DownloadPublicFile(string, string, TransformOptions?, EventHandler{float}?)</cref>
    /// </see>
    /// </summary>
    /// <param name="supabasePath"></param>
    /// <param name="localPath"></param>
    /// <param name="onProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="cacheNonce"></param>
    /// <returns></returns>
    public Task<string> Download(
        string supabasePath,
        string localPath,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? cacheNonce = null
    ) => this.Download(supabasePath, localPath, null, onProgress: onProgress, cancellationToken, cacheNonce);

    /// <summary>
    /// Downloads a byte array from a private bucket to be used programmatically. For public buckets <see>
    ///     <cref>DownloadPublicFile(string, TransformOptions?, EventHandler{float}?)</cref>
    /// </see>
    /// </summary>
    /// <param name="supabasePath"></param>
    /// <param name="transformOptions"></param>
    /// <param name="onProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="cacheNonce"></param>
    /// <returns></returns>
    public Task<byte[]> Download(
        string supabasePath,
        TransformOptions? transformOptions = null,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? cacheNonce = null
    )
    {
        var url = $"{this.Url}/object/{this.GetFinalPath(supabasePath)}";
        return this.DownloadBytes(url, transformOptions, onProgress, cancellationToken, cacheNonce);
    }

    /// <summary>
    /// Downloads a byte array from a private bucket to be used programmatically. For public buckets <see>
    ///     <cref>DownloadPublicFile(string, TransformOptions?, EventHandler{float}?)</cref>
    /// </see>
    /// </summary>
    /// <param name="supabasePath"></param>
    /// <param name="onProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="cacheNonce"></param>
    /// <returns></returns>
    public Task<byte[]> Download(string supabasePath, EventHandler<float>? onProgress = null, CancellationToken cancellationToken = default, string? cacheNonce = null) =>
        this.Download(supabasePath, transformOptions: null, onProgress: onProgress, cancellationToken, cacheNonce);

    /// <summary>
    /// Downloads a public file to the filesystem. This method DOES NOT VERIFY that the file is actually public.
    /// </summary>
    /// <param name="supabasePath"></param>
    /// <param name="localPath"></param>
    /// <param name="transformOptions"></param>
    /// <param name="onProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="cacheNonce"></param>
    /// <returns></returns>
    public Task<string> DownloadPublicFile(
        string supabasePath,
        string localPath,
        TransformOptions? transformOptions = null,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? cacheNonce = null
    )
    {
        var url = this.GetPublicUrl(supabasePath, transformOptions);
        return this.DownloadFile(url, localPath, transformOptions, onProgress, cancellationToken, cacheNonce);
    }

    /// <summary>
    /// Downloads a byte array from a private bucket to be used programmatically. This method DOES NOT VERIFY that the file is actually public.
    /// </summary>
    /// <param name="supabasePath"></param>
    /// <param name="transformOptions"></param>
    /// <param name="onProgress"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="cacheNonce"></param>
    /// <returns></returns>
    public Task<byte[]> DownloadPublicFile(
        string supabasePath,
        TransformOptions? transformOptions = null,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? cacheNonce = null
    )
    {
        var url = this.GetPublicUrl(supabasePath, transformOptions);
        return this.DownloadBytes(url, transformOptions, onProgress, cancellationToken, cacheNonce);
    }

    /// <summary>
    /// Deletes a file within the same bucket
    /// </summary>
    /// <param name="path">A path to delete, for example, `folder/image.png`.</param>
    /// <returns></returns>
    public async Task<FileObject?> Remove(string path)
    {
        var result = await this.Remove([path]);
        return result?.FirstOrDefault();
    }

    /// <summary>
    /// Deletes files within the same bucket
    /// </summary>
    /// <param name="paths">An array of files to be deleted, including the path and file name. For example [`folder/image.png`].</param>
    /// <returns></returns>
    public async Task<List<FileObject>?> Remove(List<string> paths)
    {
        var data = new Dictionary<string, object> { { "prefixes", paths } };
        var response = await Helpers.MakeRequestAsync<List<FileObject>>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Delete,
            $"{this.Url}/object/{this.BucketId}",
            data,
            this.Headers
        );

        return response;
    }

    /// <summary>
    /// Creates an upload signed URL. Use it to upload a file straight to the bucket without credentials
    /// </summary>
    /// <param name="supabasePath">The file path, including the current file name. For example, `folder/image.png`.</param>
    /// <returns></returns>
    public async Task<UploadSignedUrl> CreateUploadSignedUrl(string supabasePath)
    {
        var path = this.GetFinalPath(supabasePath);

        var url = $"{this.Url}/object/upload/sign/{path}";
        var response = await Helpers.MakeRequestAsync<CreatedUploadSignedUrlResponse>(
            this.requestClient,
            this.Options.Retry,
            HttpMethod.Post,
            url,
            null,
            this.Headers
        );

        if (
            response == null
            || string.IsNullOrEmpty(response.Url)
            || !response.Url!.Contains("token")
        )
            throw new SupabaseStorageException(
                "Response did not return with expected data. Does this token have proper permission to generate a url?"
            );

        var generatedUri = new Uri($"{this.Url}{response.Url}");
        var query = HttpUtility.ParseQueryString(generatedUri.Query);
        var token = query["token"];

        return new UploadSignedUrl(generatedUri, token, supabasePath);
    }

    /// <inheritdoc />
    public Task<GenericResponse?> PurgeCache(
        string path,
        PurgeCacheOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var url = options.ToPurgeUrl($"{this.Url}/cdn/{Helpers.EncodePath(this.GetFinalPath(path))}");
        return Helpers.MakeRequestAsync<GenericResponse>(this.requestClient, this.Options.Retry, HttpMethod.Delete, url, null, this.Headers, cancellationToken);
    }

    private async Task<string> UploadOrUpdate(
        string localPath,
        string supabasePath,
        FileOptions options,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        var uri = new Uri($"{this.Url}/object/{this.GetFinalPath(supabasePath)}");

        this.StorageHeader.Add("cache-control", $"max-age={options.CacheControl}");
        this.StorageHeader.Add("content-type", options.ContentType);

        if (options.Upsert)
            this.StorageHeader.Add("x-upsert", options.Upsert.ToString().ToLower());

        if (options.Metadata != null)
            this.StorageHeader.Add("x-metadata", ParseMetadata(options.Metadata));

        options.Headers?.ToList().ForEach(x => this.StorageHeader.Add(x.Key, x.Value));

        if (options.Duplex != null)
            this.StorageHeader.Add("x-duplex", options.Duplex.ToLower());

        var progress = new Progress<float>();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        await this.uploadClient.UploadFileAsync(uri, localPath, this.StorageHeader.Get(), progress, cancellationToken);

        return this.GetFinalPath(supabasePath);
    }

    private async Task UploadOrContinue(
        string localPath,
        string fileName,
        FileOptions options,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        var uri = new Uri($"{this.Url}/upload/resumable");

        this.StorageHeader.Add("cache-control", $"max-age={options.CacheControl}");

        var metadata = new MetadataCollection
        {
            ["bucketName"] = this.BucketId,
            ["objectName"] = fileName,
            ["contentType"] = options.ContentType,
        };

        if (options.Upsert)
            this.StorageHeader.Add("x-upsert", options.Upsert.ToString().ToLower());

        if (options.Metadata != null)
            this.StorageHeader.Add("x-metadata", ParseMetadata(options.Metadata));

        options.Headers?.ToList().ForEach(x => this.StorageHeader.Add(x.Key, x.Value));

        if (options.Duplex != null)
            this.StorageHeader.Add("x-duplex", options.Duplex.ToLower());

        var progress = new Progress<float>();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        await this.uploadClient.UploadOrContinueFileAsync(
            uri,
            localPath,
            metadata,
            this.StorageHeader.Get(),
            progress,
            cancellationToken
        );
    }

    private async Task UploadOrContinue(
        byte[] data,
        string fileName,
        FileOptions options,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        var uri = new Uri($"{this.Url}/upload/resumable");

        this.StorageHeader.Add("cache-control", $"max-age={options.CacheControl}");

        var metadata = new MetadataCollection
        {
            ["bucketName"] = this.BucketId,
            ["objectName"] = fileName,
            ["contentType"] = options.ContentType,
        };

        if (options.Upsert)
            this.StorageHeader.Add("x-upsert", options.Upsert.ToString().ToLower());

        if (options.Metadata != null)
            metadata["metadata"] = JsonSerializer.Serialize(options.Metadata, Helpers.SerializerOptions);

        options.Headers?.ToList().ForEach(x => this.StorageHeader.Add(x.Key, x.Value));

        if (options.Duplex != null)
            this.StorageHeader.Add("x-duplex", options.Duplex.ToLower());

        var progress = new Progress<float>();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        await this.uploadClient.UploadOrContinueByteAsync(
            uri,
            data,
            metadata,
            this.StorageHeader.Get(),
            progress,
            cancellationToken
        );
    }

    private static string ParseMetadata(Dictionary<string, string> metadata)
    {
        var json = JsonSerializer.Serialize(metadata, Helpers.SerializerOptions);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        return base64;
    }

    private async Task<string> UploadOrUpdate(
        byte[] data,
        string supabasePath,
        FileOptions options,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        var uri = new Uri($"{this.Url}/object/{this.GetFinalPath(supabasePath)}");

        this.StorageHeader.Add("cache-control", $"max-age={options.CacheControl}");
        this.StorageHeader.Add("content-type", options.ContentType);

        if (options.Upsert)
            this.StorageHeader.Add("x-upsert", options.Upsert.ToString().ToLower());

        if (options.Metadata != null)
            this.StorageHeader.Add("x-metadata", ParseMetadata(options.Metadata));

        options.Headers?.ToList().ForEach(x => this.StorageHeader.Add(x.Key, x.Value));

        if (options.Duplex != null)
            this.StorageHeader.Add("x-duplex", options.Duplex.ToLower());

        var progress = new Progress<float>();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        await this.uploadClient.UploadBytesAsync(uri, data, this.StorageHeader.Get(), progress, cancellationToken);

        return this.GetFinalPath(supabasePath);
    }

    private async Task<string> DownloadFile(
        string url,
        string localPath,
        TransformOptions? transformOptions = null,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? cacheNonce = null
    )
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        var builder = new UriBuilder(url);
        var progress = new Progress<float>();

        if (transformOptions is { IsEmpty: false })
            query.Add(transformOptions.ToQueryCollection());

        if (cacheNonce != null)
            query.Add("cacheNonce", cacheNonce);

        builder.Query = query.ToString();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        var stream = await this.downloadClient.DownloadDataAsync(
            builder.Uri,
            this.Headers,
            progress,
            cancellationToken
        );

        using var fileStream = new FileStream(
            localPath,
            FileMode.OpenOrCreate,
            FileAccess.Write
        );

        stream.WriteTo(fileStream);

        return localPath;
    }

    private async Task<byte[]> DownloadBytes(
        string url,
        TransformOptions? transformOptions = null,
        EventHandler<float>? onProgress = null,
        CancellationToken cancellationToken = default,
        string? cacheNonce = null
    )
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        var builder = new UriBuilder(url);
        var progress = new Progress<float>();

        if (transformOptions is { IsEmpty: false })
            query.Add(transformOptions.ToQueryCollection());

        if (cacheNonce != null)
            query.Add("cacheNonce", cacheNonce);

        builder.Query = query.ToString();

        if (onProgress != null)
            progress.ProgressChanged += onProgress;

        var stream = await this.downloadClient.DownloadDataAsync(
            builder.Uri,
            this.Headers,
            progress,
            cancellationToken
        );

        return stream.ToArray();
    }

    private string GetFinalPath(string path) => $"{this.BucketId}/{path}";
}

