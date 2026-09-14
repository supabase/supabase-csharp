using System.Net;
using System.Net.Http;
using Supabase.Core.Http;

namespace Supabase.Functions;

/// <summary>Configures a <see cref="Client"/>'s HTTP transport.</summary>
public sealed record ClientOptions
{
    /// <summary>A client to send requests through. When null, the client builds and owns its own.</summary>
    public HttpClient? HttpClient { get; init; }

    /// <summary>A proxy to route requests through. Only used when <see cref="HttpClient"/> is not supplied.</summary>
    public IWebProxy? Proxy { get; init; }

    /// <summary>Retry policy applied to each request. Default (<see cref="RetryOptions.MaxRetries"/> 0) sends once, unretried.</summary>
    public RetryOptions Retry { get; init; } = new();
}
