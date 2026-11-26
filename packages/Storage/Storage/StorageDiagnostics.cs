namespace Supabase.Storage;

/// <summary>
/// Names of the diagnostic sources the Storage client emits to. Pass these when wiring up
/// OpenTelemetry rather than repeating the name as a string literal:
/// <c>TracerProviderBuilder.AddSource(StorageDiagnostics.SourceName)</c> and
/// <c>MeterProviderBuilder.AddMeter(StorageDiagnostics.SourceName)</c>.
/// </summary>
public static class StorageDiagnostics
{
    /// <summary>
    /// The name shared by the Storage client's <see cref="System.Diagnostics.ActivitySource"/>
    /// and <see cref="System.Diagnostics.Metrics.Meter"/>.
    /// </summary>
    public const string SourceName = "supabase.storage";
}
