using Supabase.Core.ClientIdentification;

namespace Supabase.Postgrest;

/// <summary>
/// What a Postgrest request is about, as recorded on its telemetry: the logical operation,
/// either the table it targets or the function it invokes, and the SDK features it was built with.
/// </summary>
internal sealed record PostgrestCall
{
    private const string RpcOperation = "rpc";

    private PostgrestCall(string operation, string? table, string? function, SdkFeatures features)
    {
        this.Operation = operation;
        this.Table = table;
        this.Function = function;
        this.Features = features;
    }

    /// <summary>The logical operation: select, insert, update, upsert, delete or rpc.</summary>
    internal string Operation { get; }

    /// <summary>The table the request targets, or null for an RPC call.</summary>
    internal string? Table { get; }

    /// <summary>The function the request invokes, or null for a table call.</summary>
    internal string? Function { get; }

    /// <summary>The SDK features the request was built with, sent in its <c>X-Client-Info</c> header.</summary>
    internal SdkFeatures Features { get; }

    /// <summary>A request against a table, which is always built with the typed API.</summary>
    /// <param name="operation">The logical operation of the request.</param>
    /// <param name="table">The name of the table.</param>
    internal static PostgrestCall OnTable(string operation, string table) => new(operation, table, null, SdkFeatures.TypedApi);

    /// <summary>A call to a database function through the untyped API.</summary>
    /// <param name="function">The name of the function.</param>
    internal static PostgrestCall ToFunction(string function) => new(RpcOperation, null, function, SdkFeatures.None);

    /// <summary>A call to a database function whose result is read into a caller-supplied type.</summary>
    /// <param name="function">The name of the function.</param>
    internal static PostgrestCall ToTypedFunction(string function) => new(RpcOperation, null, function, SdkFeatures.TypedApi);
}
