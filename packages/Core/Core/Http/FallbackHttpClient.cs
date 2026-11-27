using System;
using System.Net.Http;
using System.Threading;

namespace Supabase.Core.Http;

/// <summary>
/// The shared <see cref="HttpClient"/> a request falls back to when the caller injected none. It is built on
/// first use rather than at type-load, so a host that injects its own client (e.g. one without System.Net
/// networking) never constructs a default client or handler.
/// </summary>
internal sealed class FallbackHttpClient
{
    private readonly Lazy<HttpClient> client;

    /// <summary>Falls back to a client from <see cref="DefaultHttpClientFactory"/>.</summary>
    internal FallbackHttpClient() : this(() => DefaultHttpClientFactory.Create()) { }

    /// <summary>Wraps <paramref name="create"/>, which runs at most once, on the first request without an injected client.</summary>
    internal FallbackHttpClient(Func<HttpClient> create) =>
        this.client = new Lazy<HttpClient>(create, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The shared fallback, creating it if needed.</summary>
    internal HttpClient Value => this.client.Value;

    /// <summary>The <paramref name="injected"/> client when there is one, else the shared fallback, creating it if needed.</summary>
    internal HttpClient Or(HttpClient? injected) => injected ?? this.Value;
}
