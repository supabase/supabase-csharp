using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Supabase.Core.Diagnostics;

/// <summary>
/// Factory methods for the <see cref="ActivitySource"/> and <see cref="Meter"/> instances used
/// by the Supabase client libraries.
///
/// Emission is zero-cost unless a consumer subscribes a listener (e.g. via the OpenTelemetry
/// SDK with <c>AddSource("supabase.*")</c> / <c>AddMeter("supabase.*")</c>), so instrumentation
/// built on these sources is always on and requires no configuration to stay silent.
/// </summary>
public static class Instrumentation
{
    /// <summary>
    /// Creates the <see cref="ActivitySource"/> for a Supabase client library, versioned from
    /// the library's assembly rather than a hardcoded literal.
    /// </summary>
    /// <param name="assembly">The assembly whose version identifies the emitting library.</param>
    /// <param name="name">The source name, e.g. <c>supabase.gotrue</c>.</param>
    public static ActivitySource CreateActivitySource(Assembly assembly, string name) => new(name, GetVersion(assembly));

    /// <summary>
    /// Creates the <see cref="Meter"/> for a Supabase client library, versioned from the
    /// library's assembly rather than a hardcoded literal.
    /// </summary>
    /// <param name="assembly">The assembly whose version identifies the emitting library.</param>
    /// <param name="name">The meter name, e.g. <c>supabase.gotrue</c>.</param>
    public static Meter CreateMeter(Assembly assembly, string name) => new(name, GetVersion(assembly));

    /// <summary>
    /// Resolves the version of an assembly, preferring the informational (package) version
    /// with any build metadata (<c>+sha</c>) stripped.
    /// </summary>
    /// <param name="assembly">The assembly whose version identifies the emitting library.</param>
    public static string GetVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
            return assembly.GetName().Version?.ToString() ?? "0.0.0";

        var metadataIndex = informational!.IndexOf('+');
        return metadataIndex > 0 ? informational.Substring(0, metadataIndex) : informational;
    }
}
