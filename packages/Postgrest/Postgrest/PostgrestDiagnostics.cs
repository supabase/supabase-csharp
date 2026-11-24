namespace Supabase.Postgrest;

/// <summary>
/// Names of the diagnostic sources the Postgrest client emits to. Pass these when wiring up
/// OpenTelemetry so you don't have to hardcode (case-sensitive) source names:
/// <c>TracerProviderBuilder.AddSource(PostgrestDiagnostics.SourceName)</c> and
/// <c>MeterProviderBuilder.AddMeter(PostgrestDiagnostics.SourceName)</c>.
/// </summary>
public static class PostgrestDiagnostics
{
    /// <summary>
    /// The name shared by the Postgrest client's <see cref="System.Diagnostics.ActivitySource"/>
    /// and <see cref="System.Diagnostics.Metrics.Meter"/>.
    /// </summary>
    public const string SourceName = "supabase.postgrest";
}
