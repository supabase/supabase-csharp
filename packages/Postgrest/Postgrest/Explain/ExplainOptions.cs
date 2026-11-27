using System.Collections.Generic;
using System.Linq;

namespace Supabase.Postgrest.Explain;

/// <summary>
/// The extra details Postgres adds to a query plan. Each one maps to the <c>EXPLAIN</c> option of the same name;
/// a detail left unset is not requested.
/// </summary>
public sealed record ExplainOptions
{
    private const string PlanType = "application/vnd.pgrst.plan+json";

    /// <summary>
    /// Runs the query and reports the actual times and row counts next to the estimates.
    /// </summary>
    public bool Analyze { get; init; }

    /// <summary>
    /// Adds the output columns of each plan node and the query identifier.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// Adds the configuration parameters that differ from their defaults and affect planning.
    /// </summary>
    public bool Settings { get; init; }

    /// <summary>
    /// Adds buffer usage. Postgres reports it for an analyzed query.
    /// </summary>
    public bool Buffers { get; init; }

    /// <summary>
    /// Adds write-ahead log record generation. Postgres reports it for an analyzed query.
    /// </summary>
    public bool Wal { get; init; }

    internal string MediaType =>
        this.RequestedDetails.Any()
            ? $"{PlanType}; options={string.Join("|", this.RequestedDetails)}"
            : PlanType;

    private IEnumerable<string> RequestedDetails
    {
        get
        {
            if (this.Analyze) yield return "analyze";
            if (this.Verbose) yield return "verbose";
            if (this.Settings) yield return "settings";
            if (this.Buffers) yield return "buffers";
            if (this.Wal) yield return "wal";
        }
    }
}
