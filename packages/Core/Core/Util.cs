using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Supabase.Core;

/// <summary>
/// Shared utilities for Supabase client libraries.
/// </summary>
public static class Util
{
    // The header is sent with every request (#523). Its host metadata (platform, runtime and framework) does not
    // depend on the client type, but building it reflects over every loaded assembly (on Unity, over each one's
    // custom attributes), so it is built once per process and shared by every client type. PublicationOnly, unlike
    // the default mode, does not cache an exception, so a build that throws is retried on the next call.
    private static readonly Lazy<string> HostMetadata = new(
        static () => BuildMetadata(RuntimeInformation.OSDescription, RuntimeInformation.IsOSPlatform, AppDomain.CurrentDomain.GetAssemblies()),
        LazyThreadSafetyMode.PublicationOnly);

    // Each client type adds only its own name and version to the shared metadata; the result is kept per type.
    private static readonly ConcurrentDictionary<Type, string> ClientInfoByType = new();

    /// <summary>
    /// Builds the <c>X-Client-Info</c> header value for the given client type.
    /// </summary>
    /// <remarks>
    /// Format: <c>name-csharp/version[; key=value ...]</c><br/>
    /// Appended metadata includes platform, platform version, runtime, runtime version,
    /// and framework (when detectable). The metadata is built once per process, on the first call,
    /// and shared by every client type; each client type's value is built on its first call and reused afterwards.
    /// </remarks>
    /// <param name="clientType">A type belonging to the client assembly, used to resolve the assembly name and version.</param>
    /// <returns>A structured header value identifying the client library and its host environment.</returns>
    public static string GetAssemblyVersion(Type clientType) =>
        ClientInfoByType.GetOrAdd(clientType, static type => ComposeHeader(type, HostMetadata.Value));

    // Testability seam: the platform and framework probes read process-wide ambient state
    // (RuntimeInformation, the loaded assembly set) that a host cannot vary at runtime. This
    // overload takes those reads as parameters and builds the metadata from them on every call, so
    // every branch is reachable hermetically; the public entry point above builds it once from the
    // real values. Internal — not part of the public API.
    internal static string GetAssemblyVersion(Type clientType, string osDescription, Func<OSPlatform, bool> isOsPlatform, IReadOnlyCollection<Assembly> loadedAssemblies) =>
        ComposeHeader(clientType, BuildMetadata(osDescription, isOsPlatform, loadedAssemblies));

    private static string ComposeHeader(Type clientType, string metadata) =>
        $"{GetClientName(clientType)}-csharp/{GetClientVersion(clientType)}{metadata}";

    private static string GetClientName(Type clientType) => clientType.Assembly.GetName().Name.ToLower();

    private static string? GetClientVersion(Type clientType) => GetInformationalVersion(clientType.Assembly);

    private static string BuildMetadata(string osDescription, Func<OSPlatform, bool> isOsPlatform, IReadOnlyCollection<Assembly> loadedAssemblies) =>
        string.Concat(GetPlatformInfo(osDescription, isOsPlatform).ToString(), GetRuntimeInfo().ToString(), GetFrameworkInfo(loadedAssemblies).ToString());

    private sealed class MetadataEntry
    {
        private readonly string key;
        private readonly string value;
        private readonly string? version;

        internal MetadataEntry(string key, string value, string? version = null)
        {
            this.key = key;
            this.value = value;
            this.version = version;
        }

        public override string ToString() => string.IsNullOrEmpty(this.version)
            ? $"; {this.key}={this.value}"
            : $"; {this.key}={this.value}; {this.key}-version={this.version}";

        internal static MetadataEntry Unknown(string key) => new(key, "unknown");
    }

    private static string GetPlatform(string osDescription, Func<OSPlatform, bool> isOsPlatform)
    {
        if (osDescription == "Browser") return "browser";
        if (isOsPlatform(OSPlatform.Windows)) return "Windows";
        if (isOsPlatform(OSPlatform.OSX)) return "macOS";
        if (isOsPlatform(OSPlatform.Create("iOS"))) return "iOS";
        if (isOsPlatform(OSPlatform.Linux)) return "Linux";
        if (isOsPlatform(OSPlatform.Create("Android"))) return "Android";
        return osDescription;
    }

    private static MetadataEntry GetPlatformInfo(string osDescription, Func<OSPlatform, bool> isOsPlatform) => new("platform", GetPlatform(osDescription, isOsPlatform), Environment.OSVersion.Version.ToString());

    private static MetadataEntry GetRuntimeInfo() => new("runtime", "dotnet", Environment.Version.ToString());

    // Priority is explicit: MAUI wins over Blazor in hybrid apps where both assemblies are present.
    // Unity version uses GetCustomAttributesData() rather than member reflection — safe under IL2CPP.
    private static MetadataEntry GetFrameworkInfo(IReadOnlyCollection<Assembly> loadedAssemblies)
    {
        var assemblies = loadedAssemblies
            .GroupBy(a => a.GetName().Name)
            .ToDictionary(g => g.Key, g => g.First());

        if (assemblies.TryGetValue("Microsoft.Maui", out var maui))
            return new MetadataEntry("framework", "maui", GetInformationalVersion(maui));
        if (assemblies.ContainsKey("UnityEngine.CoreModule"))
            return new MetadataEntry("framework", "unity", GetUnityVersion(loadedAssemblies));
        if (assemblies.TryGetValue("Microsoft.AspNetCore.Components", out var blazor))
            return new MetadataEntry("framework", "blazor", GetInformationalVersion(blazor));
        return MetadataEntry.Unknown("framework");
    }

    private static string? GetInformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    // Defensive: GetCustomAttributesData() can throw on exotic hosts (dynamic/reflection-emit assemblies); not reliably triggerable in a unit test.
    [ExcludeFromCodeCoverage]
    private static CustomAttributeData[] SafeGetCustomAttributesData(Assembly assembly)
    {
        try { return assembly.GetCustomAttributesData().ToArray(); }
        catch { return []; }
    }

    private static string? GetUnityVersion(IReadOnlyCollection<Assembly> loadedAssemblies)
    {
        var attr = loadedAssemblies
            .SelectMany(SafeGetCustomAttributesData)
            .FirstOrDefault(d => d.AttributeType.Name == "UnityAPICompatibilityVersionAttribute");
        return attr?.ConstructorArguments.Count > 0 ? attr.ConstructorArguments[0].Value as string : null;
    }
}
