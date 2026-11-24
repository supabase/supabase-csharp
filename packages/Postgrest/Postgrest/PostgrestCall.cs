namespace Supabase.Postgrest;

/// <summary>
/// What a Postgrest request is about, as recorded on its telemetry: the logical operation and
/// either the table it targets or the function it invokes.
/// </summary>
internal sealed record PostgrestCall
{
    private PostgrestCall(string operation, string? table, string? function)
    {
        this.Operation = operation;
        this.Table = table;
        this.Function = function;
    }

    /// <summary>The logical operation: select, insert, update, upsert, delete or rpc.</summary>
    internal string Operation { get; }

    /// <summary>The table the request targets, or null for an RPC call.</summary>
    internal string? Table { get; }

    /// <summary>The function the request invokes, or null for a table call.</summary>
    internal string? Function { get; }

    /// <summary>A request against a table.</summary>
    /// <param name="operation">The logical operation of the request.</param>
    /// <param name="table">The name of the table.</param>
    internal static PostgrestCall OnTable(string operation, string table) => new(operation, table, null);

    /// <summary>A call to a database function.</summary>
    /// <param name="function">The name of the function.</param>
    internal static PostgrestCall ToFunction(string function) => new("rpc", null, function);
}
