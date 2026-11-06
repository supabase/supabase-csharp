using System;
using System.Collections.Generic;

namespace Supabase.Core.Http;

/// <summary>Retry policy applied by <see cref="RetryExecutor"/> around a request.</summary>
public sealed record RetryOptions
{
    /// <summary>Retry attempts after the initial request. Default 0 preserves today's no-retry behavior.</summary>
    public int MaxRetries { get; init; }

    /// <summary>Delay before the first retry; later attempts back off exponentially, capped at <see cref="MaxDelay"/>.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Upper bound on the backoff delay between attempts.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Randomizes each backoff delay between zero and its exponential value to avoid retry lockstep.</summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>HTTP status codes treated as transient and worth retrying.</summary>
    public IReadOnlyCollection<int> RetryableStatusCodes { get; init; } = new HashSet<int> { 408, 429, 500, 502, 503, 504 };

    /// <summary>
    /// The clock the backoff delay between attempts waits on, and that per-request timeouts (Postgrest
    /// <c>Timeout</c>, Functions <c>HttpTimeout</c>) are measured on. Defaults to <see cref="System.TimeProvider.System"/>;
    /// supply another provider to drive them from a host's own timers (or a fake clock in tests).
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
