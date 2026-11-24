namespace Supabase.Gotrue;

/// <summary>
///     Names of the diagnostic sources the GoTrue client emits to. Pass these when wiring up
///     OpenTelemetry so you don't have to hardcode (case-sensitive) source names:
///     <c>TracerProviderBuilder.AddSource(GotrueDiagnostics.SourceName)</c> and
///     <c>MeterProviderBuilder.AddMeter(GotrueDiagnostics.SourceName)</c>.
/// </summary>
public static class GotrueDiagnostics
{
    /// <summary>
    ///     The name shared by the GoTrue client's <see cref="System.Diagnostics.ActivitySource" />
    ///     and <see cref="System.Diagnostics.Metrics.Meter" />.
    /// </summary>
    public const string SourceName = "supabase.gotrue";
}