using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Core.Http;

namespace Supabase.Functions;

/// <summary>Configures a <see cref="Client"/>'s HTTP transport.</summary>
public sealed record ClientOptions
{
    /// <summary>A client to send requests through. When null, the client builds and owns its own.</summary>
    public HttpClient? HttpClient { get; init; }

    /// <summary>A proxy to route requests through. Only used when <see cref="HttpClient"/> is not supplied.</summary>
    public IWebProxy? Proxy { get; init; }

    /// <summary>
    /// Supplies the access token sent as <c>Authorization: Bearer</c> in place of the <see cref="Client.SetAuth"/>
    /// token and any Authorization from <see cref="Client.GetHeaders"/>. Awaited before every invocation that
    /// carries neither a token argument nor an Authorization header of its own; a null token leaves the
    /// headers unchanged.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? RetrieveAccessToken { get; init; }

    /// <summary>Retry policy applied to each request. Default (<see cref="RetryOptions.MaxRetries"/> 0) sends once, unretried.</summary>
    public RetryOptions Retry { get; init; } = new();

    /// <summary>
    /// Whether the SDK adds trace context (<c>traceparent</c>, <c>tracestate</c>, <c>baggage</c>) to each
    /// request: the current activity's, or a new unsampled <c>traceparent</c> when there is none. Defaults to
    /// <see cref="TracePropagation.Enabled"/>. <see cref="TracePropagation.Disabled"/> stops the SDK from injecting it; the .NET runtime's own injection is controlled through
    /// <see cref="System.Diagnostics.DistributedContextPropagator.Current"/>.
    /// </summary>
    public TracePropagation TracePropagation { get; init; } = TracePropagation.Enabled;

    /// <summary>
    /// Receives the diagnostics of this client: request outcomes and retry decisions at <c>Debug</c>, and failures of work that
    /// runs in the background at <c>Warning</c>. Left null, the client logs nothing. Never carries credentials, tokens, bodies,
    /// query strings or header values.
    /// </summary>
    public Microsoft.Extensions.Logging.ILogger? Logger { get; init; }

    /// <summary>
    /// The clock the retry backoff and each invocation's <see cref="Client.InvokeFunctionOptions.HttpTimeout"/> wait on. Defaults to
    /// <see cref="System.TimeProvider.System"/>; supply another provider to run them from a host's own timers, such as a
    /// frame loop where OS timers never fire.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
