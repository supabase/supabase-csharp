using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Supabase;

internal static class StorageConfiguration
{
    internal static Storage.ClientOptions For(SupabaseOptions options, Func<CancellationToken, Task<string?>>? accessToken) =>
        options.HttpClient is null && accessToken is null
            ? options.StorageClientOptions
            : Copy(options.StorageClientOptions, options.HttpClient, accessToken);

    private static Storage.ClientOptions Copy(Storage.ClientOptions options, HttpClient? sharedClient, Func<CancellationToken, Task<string?>>? accessToken) =>
        new()
        {
            HttpUploadTimeout = options.HttpUploadTimeout,
            HttpDownloadTimeout = options.HttpDownloadTimeout,
            HttpRequestTimeout = options.HttpRequestTimeout,
            HttpRequestClient = options.HttpRequestClient ?? sharedClient,
            HttpUploadClient = options.HttpUploadClient ?? sharedClient,
            HttpDownloadClient = options.HttpDownloadClient ?? sharedClient,
            Proxy = options.Proxy,
            Retry = options.Retry,
            RetrieveAccessToken = accessToken ?? options.RetrieveAccessToken,
        };
}
