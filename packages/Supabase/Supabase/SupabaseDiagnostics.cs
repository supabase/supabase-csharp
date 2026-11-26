using System.Collections.Generic;
using Supabase.Functions;
using Supabase.Gotrue;
using Supabase.Postgrest;
using Supabase.Storage;

namespace Supabase;

/// <summary>
/// Aggregates the diagnostic source names emitted by the Supabase clients so consumers of the
/// meta-package can register them all with OpenTelemetry in one place, without importing each
/// sub-package's namespace or repeating the names as string literals.
///
/// Each client uses the same name for its <see cref="System.Diagnostics.ActivitySource"/> and
/// its <see cref="System.Diagnostics.Metrics.Meter"/>, so <see cref="SourceNames"/> works for
/// both <c>TracerProviderBuilder.AddSource(...)</c> and <c>MeterProviderBuilder.AddMeter(...)</c>.
/// Realtime is not yet instrumented and is therefore not included.
/// </summary>
public static class SupabaseDiagnostics
{
    /// <summary>
    /// The names of every <see cref="System.Diagnostics.ActivitySource"/> and
    /// <see cref="System.Diagnostics.Metrics.Meter"/> emitted by the Supabase clients.
    /// </summary>
    public static IReadOnlyList<string> SourceNames { get; } = new[]
    {
        GotrueDiagnostics.SourceName,
        PostgrestDiagnostics.SourceName,
        FunctionsDiagnostics.SourceName,
        StorageDiagnostics.SourceName,
    };
}
