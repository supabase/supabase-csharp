using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Supabase.Postgrest.LimitAffectedRows;

internal sealed record AffectedRowsLimit
{
    private static readonly HttpMethod Patch = new("PATCH");

    private AffectedRowsLimit(int rows) => this.Rows = rows;

    internal int Rows { get; }

    internal static AffectedRowsLimit Of(int rows) =>
        rows < 0
            ? throw new ArgumentOutOfRangeException(nameof(rows), rows, "The maximum number of affected rows cannot be negative.")
            : new AffectedRowsLimit(rows);

    internal void ApplyTo(HttpMethod method, Dictionary<string, string> headers)
    {
        EnsureMutation(method);
        headers["Prefer"] = $"{headers["Prefer"]},{this.Preference}";
    }

    private string Preference => $"handling=strict,max-affected={this.Rows}";

    private static void EnsureMutation(HttpMethod method)
    {
        if (method != Patch && method != HttpMethod.Delete)
            throw new InvalidOperationException("MaxAffected only applies to Update and Delete.");
    }
}
