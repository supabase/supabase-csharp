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
        var handler = CreateHandler(proxy);
        return timeout.HasValue ? new HttpClient(handler) { Timeout = timeout.Value } : new HttpClient(handler);
    }

    /// <summary>
    /// Builds the handler, wiring the proxy only when one is supplied. Some platforms reject proxy
    /// configuration outright: Blazor WASM's <c>BrowserHttpHandler</c> throws
    /// <see cref="PlatformNotSupportedException"/> from the <c>Proxy</c> and <c>UseProxy</c> setters
    /// regardless of the assigned value, so leaving them untouched when there is no proxy is what
    /// keeps construction from throwing there. When a proxy is set, the handler's default
    /// <c>UseProxy</c> (<c>true</c>) already routes through it.
    /// </summary>
    internal static HttpClientHandler CreateHandler(IWebProxy? proxy)
    {
        var handler = new HttpClientHandler();
        if (proxy != null)
            handler.Proxy = proxy;
        return handler;
    }
}
