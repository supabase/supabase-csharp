namespace Supabase.Functions;

/// <summary>
/// Names of the diagnostic sources the Functions client emits to. Pass these when wiring up
/// OpenTelemetry so you don't have to hardcode (case-sensitive) source names:
/// <c>TracerProviderBuilder.AddSource(FunctionsDiagnostics.SourceName)</c> and
/// <c>MeterProviderBuilder.AddMeter(FunctionsDiagnostics.SourceName)</c>.
/// </summary>
public static class FunctionsDiagnostics
{
    /// <summary>
    /// The name shared by the Functions client's <see cref="System.Diagnostics.ActivitySource"/>
    /// and <see cref="System.Diagnostics.Metrics.Meter"/>.
    /// </summary>
    public const string SourceName = "supabase.functions";
}
