using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Supabase.Core.Http;
using Supabase.Storage;

namespace Supabase;

internal static class StorageConfiguration
{
    internal static Storage.ClientOptions For(SupabaseOptions options, Func<CancellationToken, Task<string?>>? accessToken) =>
        For(options, accessToken, LoggerFor(options));

    private static Storage.ClientOptions For(SupabaseOptions options, Func<CancellationToken, Task<string?>>? accessToken, ILogger? logger) =>
        options.HttpClient is null && accessToken is null && TracePropagationFor(options) == options.StorageClientOptions.TracePropagation
            && logger == options.StorageClientOptions.Logger
            ? options.StorageClientOptions
            : Copy(options.StorageClientOptions, options.HttpClient, accessToken, TracePropagationFor(options), logger);

    private static TracePropagation TracePropagationFor(SupabaseOptions options) =>
        options.TracePropagation == TracePropagation.Disabled ? TracePropagation.Disabled : options.StorageClientOptions.TracePropagation;

    private static ILogger? LoggerFor(SupabaseOptions options) =>
        options.StorageClientOptions.Logger ?? options.LoggerFor(StorageDiagnostics.SourceName);

    private static Storage.ClientOptions Copy(Storage.ClientOptions options, HttpClient? sharedClient, Func<CancellationToken, Task<string?>>? accessToken,
        TracePropagation tracePropagation, ILogger? logger) =>
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
            Logger = logger,
            RetrieveAccessToken = accessToken ?? options.RetrieveAccessToken,
        };
}
