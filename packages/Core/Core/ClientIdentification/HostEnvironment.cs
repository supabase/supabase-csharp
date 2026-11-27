using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// The process-wide ambient reads behind the <c>X-Client-Info</c> header: OS, processor architecture,
/// runtime, loaded assemblies and, inside a Unity host, the engine's own view of version and platform.
/// </summary>
/// <remarks>
/// Testability seam: a host cannot vary these at runtime, so <see cref="ClientEnvironment"/> takes them as a value
/// and every platform/architecture/framework branch is reachable hermetically. Internal, not public API.
/// </remarks>
internal sealed record HostEnvironment(
    string OsDescription,
    Func<OSPlatform, bool> IsOsPlatform,
    string OsVersion,
    string OsArchitecture,
    string RuntimeVersion,
    IReadOnlyCollection<Assembly> LoadedAssemblies,
    Func<string?> UnityVersion,
    Func<string?> UnityPlatform)
{
    [ExcludeFromCodeCoverage]
    internal static HostEnvironment Current() => new(
        RuntimeInformation.OSDescription,
        RuntimeInformation.IsOSPlatform,
        Environment.OSVersion.Version.ToString(),
        RuntimeInformation.OSArchitecture.ToString(),
        Environment.Version.ToString(),
        AppDomain.CurrentDomain.GetAssemblies(),
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
