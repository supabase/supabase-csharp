using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Supabase.Core.Http;

namespace Supabase.Storage;

/// <summary>
/// Options that can be passed into the Storage Client
/// </summary>
public class ClientOptions
{
    /// <summary>
    /// The timespan to wait before an HTTP Upload Timesout
    /// See: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-7.0
    /// </summary>
    public TimeSpan HttpUploadTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// The timespan to wait before an HTTP Upload Timesout
    /// See: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-7.0
    /// </summary>
    public TimeSpan HttpDownloadTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// The timespan to wait before an HTTP Client request times out.
    /// See: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-7.0
    /// </summary>
    public TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(100);

    /// <summary>An HttpClient to send metadata/API requests (list, create, delete, sign, etc.) through. When null, the client builds and owns its own, honoring <see cref="HttpRequestTimeout"/>.</summary>
    public HttpClient? HttpRequestClient { get; set; }

    /// <summary>An HttpClient to send file uploads through. When null, the client builds and owns its own, honoring <see cref="HttpUploadTimeout"/>.</summary>
    public HttpClient? HttpUploadClient { get; set; }

    /// <summary>An HttpClient to send file downloads through. When null, the client builds and owns its own, honoring <see cref="HttpDownloadTimeout"/>.</summary>
    public HttpClient? HttpDownloadClient { get; set; }

    /// <summary>A proxy to route requests through. Only applies to clients this options object builds itself (i.e. when the corresponding HttpXClient above is not supplied).</summary>
    public IWebProxy? Proxy { get; set; }

    /// <summary>
    /// Retry policy applied to metadata/API requests (list, create, delete, sign, etc.). Not applied to
    /// uploads or downloads, where replaying a partially-consumed transfer stream would be unsafe — use
    /// the resumable upload APIs for interrupted transfers instead. Default (<see cref="RetryOptions.MaxRetries"/> 0)
    /// sends once, unretried.
    /// </summary>
    public RetryOptions Retry { get; set; } = new RetryOptions();
}
