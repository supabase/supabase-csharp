using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Supabase.Core;

/// <summary>
/// The process-wide ambient reads behind the <c>X-Client-Info</c> header: OS, runtime, loaded
/// assemblies and, inside a Unity host, the engine's own view of version and platform.
/// </summary>
/// <remarks>
/// Testability seam: a host cannot vary these at runtime, so <see cref="Util"/> takes them as a value
/// and every platform/runtime/framework branch is reachable hermetically. Internal, not public API.
/// </remarks>
internal sealed record HostEnvironment(
    string OsDescription,
    Func<OSPlatform, bool> IsOsPlatform,
    IReadOnlyCollection<Assembly> LoadedAssemblies,
    string FrameworkDescription,
    Func<string?> UnityVersion,
    Func<string?> UnityPlatform)
{
    [ExcludeFromCodeCoverage]
    internal static HostEnvironment Current() => new(
        RuntimeInformation.OSDescription,
        RuntimeInformation.IsOSPlatform,
        AppDomain.CurrentDomain.GetAssemblies(),
        RuntimeInformation.FrameworkDescription,
        () => ReadUnityApplicationProperty("unityVersion") as string,
        () => ReadUnityApplicationProperty("platform")?.ToString());

    [ExcludeFromCodeCoverage]
    private static object? ReadUnityApplicationProperty(string name)
    {
        try
        {
            return Type.GetType("UnityEngine.Application, UnityEngine.CoreModule", throwOnError: false)
                ?.GetProperty(name, BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
        }
        catch
        {
            return null;
        }
    }
}
