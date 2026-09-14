using System;
using System.Net;
using System.Net.Http;

namespace Supabase.Core.Http;

/// <summary>Builds the fallback <see cref="HttpClient"/> a package uses when a consumer hasn't injected one.</summary>
public static class DefaultHttpClientFactory
{
    /// <summary>Creates a client with the given timeout (BCL default, 100s, when null) and optional proxy.</summary>
    public static HttpClient Create(TimeSpan? timeout = null, IWebProxy? proxy = null)
    {
        var handler = new HttpClientHandler { Proxy = proxy, UseProxy = proxy != null };
        return timeout.HasValue ? new HttpClient(handler) { Timeout = timeout.Value } : new HttpClient(handler);
    }
}
