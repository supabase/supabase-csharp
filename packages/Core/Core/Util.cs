using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Supabase.Core;

/// <summary>
/// Shared utilities for Supabase client libraries.
/// </summary>
public static class Util
{
    /// <summary>
    /// Builds the <c>X-Client-Info</c> header value for the given client type.
    /// </summary>
    /// <remarks>
    /// Format: <c>name-csharp/version[; key=value ...]</c><br/>
    /// Appended metadata includes platform, platform version, runtime, runtime version,
    /// and framework (when detectable).
    /// </remarks>
    /// <param name="clientType">A type belonging to the client assembly, used to resolve the assembly name and version.</param>
    /// <returns>A structured header value identifying the client library and its host environment.</returns>
    public static string GetAssemblyVersion(Type clientType) =>
        ClientInfoCache.GetOrAdd(clientType.Assembly, static (_, type) => BuildClientInfo(type), clientType);

    // The header is process-invariant, yet callers stamp it on every request; building it scans
    // every loaded assembly, so it is built once per client assembly.
    private static readonly ConcurrentDictionary<Assembly, string> ClientInfoCache = new();

    private static string BuildClientInfo(Type clientType) => GetAssemblyVersion(clientType, HostEnvironment.Current());

    internal static string GetAssemblyVersion(Type clientType, HostEnvironment host) =>
        $"{GetClientName(clientType)}-csharp/{GetClientVersion(clientType)}{BuildMetadata(host)}";

    private static string GetClientName(Type clientType) => clientType.Assembly.GetName().Name.ToLower();

    private static string? GetClientVersion(Type clientType) => GetInformationalVersion(clientType.Assembly);

    private static string BuildMetadata(HostEnvironment host)
    {
        var framework = GetFrameworkInfo(host);
        var isUnityHost = framework.Value == "unity";
        return string.Concat(GetPlatformInfo(host, isUnityHost).ToString(), GetRuntimeInfo(host, isUnityHost).ToString(), framework.ToString());
    }

    private sealed class MetadataEntry
    {
        private readonly string key;
        private readonly string? version;

        internal MetadataEntry(string key, string value, string? version = null)
        {
            this.key = key;
            this.Value = value;
            this.version = version;
        }

        internal string Value { get; }

        public override string ToString() => string.IsNullOrEmpty(this.version)
            ? $"; {this.key}={this.Value}"
            : $"; {this.key}={this.Value}; {this.key}-version={this.version}";

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

    private static string? GetUnityPlatform(HostEnvironment host) =>
        host.UnityPlatform() is { } runtimePlatform ? ToPlatformName(runtimePlatform) : null;

    private static string ToPlatformName(string unityRuntimePlatform) => unityRuntimePlatform switch
    {
        "IPhonePlayer" => "iOS",
        "WebGLPlayer" => "browser",
        _ when StartsWithAny(unityRuntimePlatform, "Windows", "WSAPlayer") => "Windows",
        _ when StartsWithAny(unityRuntimePlatform, "OSX") => "macOS",
        _ when StartsWithAny(unityRuntimePlatform, "Linux", "EmbeddedLinux") => "Linux",
        _ => unityRuntimePlatform,
    };

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));

    private static MetadataEntry GetPlatformInfo(HostEnvironment host, bool isUnityHost)
    {
        var platform = (isUnityHost ? GetUnityPlatform(host) : null) ?? GetPlatform(host.OsDescription, host.IsOsPlatform);
        return new MetadataEntry("platform", platform, Environment.OSVersion.Version.ToString());
    }

    private static MetadataEntry GetRuntimeInfo(HostEnvironment host, bool isUnityHost) =>
        (isUnityHost ? GetUnityRuntimeInfo(host.FrameworkDescription) : null) ?? new MetadataEntry("runtime", "dotnet", Environment.Version.ToString());

    private const string MonoPrefix = "Mono ";

    private static MetadataEntry? GetUnityRuntimeInfo(string frameworkDescription) => frameworkDescription switch
    {
        _ when frameworkDescription.Contains("IL2CPP", StringComparison.OrdinalIgnoreCase) => new MetadataEntry("runtime", "il2cpp"),
        _ when frameworkDescription.StartsWith(MonoPrefix, StringComparison.Ordinal) => new MetadataEntry("runtime", "mono", ParseMonoVersion(frameworkDescription)),
        _ => null,
    };

    private static string ParseMonoVersion(string frameworkDescription) =>
        frameworkDescription.Substring(MonoPrefix.Length).Split(' ')[0];

    // Priority is explicit: MAUI wins over Blazor in hybrid apps where both assemblies are present.
    private static MetadataEntry GetFrameworkInfo(HostEnvironment host)
    {
        var assemblies = host.LoadedAssemblies
            .GroupBy(a => a.GetName().Name)
            .ToDictionary(g => g.Key, g => g.First());

        if (assemblies.TryGetValue("Microsoft.Maui", out var maui))
            return new MetadataEntry("framework", "maui", GetInformationalVersion(maui));
        if (assemblies.ContainsKey("UnityEngine.CoreModule"))
            return new MetadataEntry("framework", "unity", host.UnityVersion());
        if (assemblies.TryGetValue("Microsoft.AspNetCore.Components", out var blazor))
            return new MetadataEntry("framework", "blazor", GetInformationalVersion(blazor));
        return MetadataEntry.Unknown("framework");
    }

    private static string? GetInformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
