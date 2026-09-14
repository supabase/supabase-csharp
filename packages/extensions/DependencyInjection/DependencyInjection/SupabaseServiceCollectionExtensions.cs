using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Supabase.Extensions.DependencyInjection;

/// <summary>
/// Registers a <see cref="Supabase.Client"/> — and each of its sub-clients individually — against
/// <see cref="IHttpClientFactory"/>-managed, pooled <see cref="HttpClient"/>s. This is the fix for the
/// socket/connection-pool exhaustion that an SDK building its own <see cref="HttpClient"/> per instance
/// causes under sustained ASP.NET Core load.
///
/// This package adds no new injection seams of its own: <see cref="SupabaseOptions.HttpClient"/>,
/// <see cref="SupabaseOptions.Proxy"/>, the per-package retry options, and Storage's three named
/// clients on <see cref="Storage.ClientOptions"/> already exist. <see cref="AddSupabase"/> only wires
/// <see cref="IHttpClientFactory"/>-created clients into those existing seams and calls the
/// already-public <see cref="Client(string, string?, SupabaseOptions?)"/> constructor.
/// </summary>
public static class SupabaseServiceCollectionExtensions
{
    private const string HttpClientName = "Supabase";
    private const string StorageRequestHttpClientName = "Supabase.Storage.Request";
    private const string StorageUploadHttpClientName = "Supabase.Storage.Upload";
    private const string StorageDownloadHttpClientName = "Supabase.Storage.Download";

    /// <summary>
    /// Registers a scoped <see cref="Supabase.Client"/>, plus each of its sub-clients individually
    /// (<c>IGotrueClient&lt;User, Session&gt;</c>, <c>IPostgrestClient</c>, <c>IStorageClient&lt;Bucket, FileObject&gt;</c>,
    /// <c>IFunctionsClient</c>, <c>IRealtimeClient&lt;RealtimeSocket, RealtimeChannel&gt;</c>) so a handler can inject
    /// just the one it needs instead of the whole umbrella client.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="supabaseUrl">The project's Supabase URL, e.g. "https://xyz.supabase.co".</param>
    /// <param name="supabaseKey">The project's anon or service-role API key.</param>
    /// <param name="configureOptions">
    /// Configures the <see cref="SupabaseOptions"/> passed to every sub-client. Any
    /// <see cref="SupabaseOptions.HttpClient"/> or <see cref="Storage.ClientOptions"/> HttpXClient
    /// properties set here are overwritten — <see cref="AddSupabase"/> supplies those from
    /// <see cref="IHttpClientFactory"/> so they come from the pooled, DI-managed handlers instead.
    /// </param>
    public static IServiceCollection AddSupabase(
        this IServiceCollection services,
        string supabaseUrl,
        string supabaseKey,
        Action<SupabaseOptions>? configureOptions = null)
    {
        // Read once, purely for the static (non-per-request) proxy/timeout config below — never handed
        // to a Client instance. The Client actually constructed per scope gets its own fresh
        // SupabaseOptions further down, so concurrent scopes never race over shared mutable options.
        var seed = new SupabaseOptions();
        configureOptions?.Invoke(seed);

        services.AddHttpClient(HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(seed.Proxy));

        services.AddHttpClient(StorageRequestHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(seed.StorageClientOptions.Proxy))
            .ConfigureHttpClient(c => c.Timeout = seed.StorageClientOptions.HttpRequestTimeout);

        services.AddHttpClient(StorageUploadHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(seed.StorageClientOptions.Proxy))
            .ConfigureHttpClient(c => c.Timeout = seed.StorageClientOptions.HttpUploadTimeout);

        services.AddHttpClient(StorageDownloadHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(seed.StorageClientOptions.Proxy))
            .ConfigureHttpClient(c => c.Timeout = seed.StorageClientOptions.HttpDownloadTimeout);

        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var options = new SupabaseOptions();
            configureOptions?.Invoke(options);

            options.HttpClient = factory.CreateClient(HttpClientName);
            options.StorageClientOptions.HttpRequestClient = factory.CreateClient(StorageRequestHttpClientName);
            options.StorageClientOptions.HttpUploadClient = factory.CreateClient(StorageUploadHttpClientName);
            options.StorageClientOptions.HttpDownloadClient = factory.CreateClient(StorageDownloadHttpClientName);

            return new Client(supabaseUrl, supabaseKey, options);
        });

        services.AddScoped(sp => sp.GetRequiredService<Client>().Auth);
        services.AddScoped(sp => sp.GetRequiredService<Client>().Postgrest);
        services.AddScoped(sp => sp.GetRequiredService<Client>().Storage);
        services.AddScoped(sp => sp.GetRequiredService<Client>().Functions);
        services.AddScoped(sp => sp.GetRequiredService<Client>().Realtime);

        return services;
    }

    private static HttpClientHandler CreateHandler(IWebProxy? proxy) =>
        new() { Proxy = proxy, UseProxy = proxy != null };
}
