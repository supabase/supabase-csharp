using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Supabase.Core.ClientIdentification;

/// <summary>
/// The environment the SDK runs in, as the <c>X-Client-Info</c> parameters describe it: platform, its version and
/// processor architecture, the .NET runtime and its version, and the application framework when one is detected.
/// </summary>
internal sealed record ClientEnvironment(
    string Platform,
    string? PlatformVersion,
    string? PlatformArchitecture,
    string RuntimeVersion,
    HostFramework? Framework)
{
    private const string Runtime = "dotnet";
    private const string Browser = "browser";

    private static readonly IReadOnlyDictionary<string, string> GoArchitectureNames = new Dictionary<string, string>
    {
        ["X86"] = "386",
        ["X64"] = "amd64",
        ["Arm"] = "arm",
        ["Armv6"] = "arm",
        ["Arm64"] = "arm64",
        ["Wasm"] = "wasm",
        ["S390x"] = "s390x",
        ["LoongArch64"] = "loong64",
        ["Ppc64le"] = "ppc64le",
        ["RiscV64"] = "riscv64",
    };

    internal static ClientEnvironment Detect(HostEnvironment host)
    {
        var framework = HostFramework.Detect(host);
        var platform = PlatformOf(host, framework);
        return new ClientEnvironment(platform, PlatformVersionOf(platform, host), ArchitectureOf(host), host.RuntimeVersion, framework);
    }

    internal IReadOnlyList<ClientInfoParameter> Parameters() => new[]
    {
        ClientInfoParameter.Of("platform", this.Platform),
        ClientInfoParameter.Of("platform-version", this.PlatformVersion),
        ClientInfoParameter.Of("platform-architecture", this.PlatformArchitecture),
        ClientInfoParameter.Of("runtime", Runtime),
        ClientInfoParameter.Of("runtime-version", this.RuntimeVersion),
        ClientInfoParameter.Of("framework", this.Framework?.Name),
        ClientInfoParameter.Of("framework-version", this.Framework?.Version),
    }.OfType<ClientInfoParameter>().ToList();

    private static string PlatformOf(HostEnvironment host, HostFramework? framework) =>
        (framework is { IsUnity: true } && host.UnityPlatform() is { } unityPlatform ? UnityPlatformName(unityPlatform) : null)
        ?? OsPlatformName(host);

    private static string OsPlatformName(HostEnvironment host) => host switch
    {
        { OsDescription: "Browser" } => Browser,
        _ when host.IsOsPlatform(OSPlatform.Windows) => "Windows",
        _ when host.IsOsPlatform(OSPlatform.OSX) => "macOS",
        _ when host.IsOsPlatform(OSPlatform.Create("iOS")) => "iOS",
        _ when host.IsOsPlatform(OSPlatform.Linux) => "Linux",
        _ when host.IsOsPlatform(OSPlatform.Create("Android")) => "Android",
        _ when host.IsOsPlatform(OSPlatform.Create("FreeBSD")) => "FreeBSD",
        _ => FirstWord(host.OsDescription),
    };

    private static string UnityPlatformName(string unityRuntimePlatform) => unityRuntimePlatform switch
    {
        "IPhonePlayer" => "iOS",
        "WebGLPlayer" => Browser,
        _ when StartsWithAny(unityRuntimePlatform, "Windows", "WSAPlayer") => "Windows",
        _ when StartsWithAny(unityRuntimePlatform, "OSX") => "macOS",
        _ when StartsWithAny(unityRuntimePlatform, "Linux", "EmbeddedLinux") => "Linux",
        _ => unityRuntimePlatform,
    };

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));

    private static string FirstWord(string value) => value.Trim().Split(' ')[0];

    private static string? PlatformVersionOf(string platform, HostEnvironment host) => platform == Browser ? null : host.OsVersion;

    private static string? ArchitectureOf(HostEnvironment host) =>
        GoArchitectureNames.TryGetValue(host.OsArchitecture, out var goArchitecture) ? goArchitecture : null;
}
