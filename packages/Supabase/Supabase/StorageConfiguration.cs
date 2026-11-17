using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Http;

namespace Supabase;

internal static class StorageConfiguration
{
    internal static Storage.ClientOptions For(SupabaseOptions options, Func<CancellationToken, Task<string?>>? accessToken) =>
        options.HttpClient is null && accessToken is null && TracePropagationFor(options) == options.StorageClientOptions.TracePropagation
            ? options.StorageClientOptions
            : Copy(options.StorageClientOptions, options.HttpClient, accessToken, TracePropagationFor(options));

    private static TracePropagation TracePropagationFor(SupabaseOptions options) =>
        options.TracePropagation == TracePropagation.Disabled ? TracePropagation.Disabled : options.StorageClientOptions.TracePropagation;

    private static Storage.ClientOptions Copy(Storage.ClientOptions options, HttpClient? sharedClient, Func<CancellationToken, Task<string?>>? accessToken,
        TracePropagation tracePropagation) =>
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
            TracePropagation = tracePropagation,
            RetrieveAccessToken = accessToken ?? options.RetrieveAccessToken,
        };
}
