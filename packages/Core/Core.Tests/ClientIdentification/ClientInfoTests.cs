using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Core.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.ClientIdentification;

namespace Core.Tests.ClientIdentification;

/// <summary>
/// The SDK's own <c>X-Client-Info</c> value: the product token, then the platform, runtime and framework parameters,
/// each trimmed, percent-encoded and left out when it cannot be determined (client_info spec, Format and Parameters).
/// Driven through the <see cref="HostEnvironment"/> seam so every host branch is reachable on any machine.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ClientInfoTests
{
    private const string OsVersion = "15.1.0";
    private const string RuntimeVersion = "10.0.8";

    private static readonly Assembly Sdk = FakeAssembly.Named("Supabase.Gotrue", "8.14.0");

    private static readonly HostEnvironment PlainHost = new(
        OsDescription: "any",
        IsOsPlatform: _ => false,
        OsVersion: OsVersion,
        OsArchitecture: "Arm64",
        RuntimeVersion: RuntimeVersion,
        LoadedAssemblies: [],
        UnityVersion: () => null,
        UnityPlatform: () => null);

    private static readonly HostEnvironment UnityHost = PlainHost with { LoadedAssemblies = [FakeAssembly.Named("UnityEngine.CoreModule")] };

    [TestMethod]
    public void HeaderValue_ShouldDescribeTheSdkAndItsEnvironment() =>
        Header(PlainHost with { IsOsPlatform = OnlyPlatform(OSPlatform.OSX) }).Should().Be(
            $"supabase-gotrue-csharp/8.14.0; platform=macOS; platform-version={OsVersion}; platform-architecture=arm64; runtime=dotnet; runtime-version={RuntimeVersion}");

    [TestMethod]
    [DataRow("Supabase", "supabase-csharp")]
    [DataRow("Supabase.Postgrest", "supabase-postgrest-csharp")]
    public void HeaderValue_ShouldNameTheSdkInKebabCase_GivenClientAssembly(string assemblyName, string expected) =>
        Header(PlainHost, FakeAssembly.Named(assemblyName, "1.0.0")).Should().StartWith($"{expected}/1.0.0; ",
            "the name is lowercase kebab-case (client_info spec, Format)");

    [TestMethod]
    public void HeaderValue_ShouldDropBuildMetadataFromTheVersion_GivenSourceLinkInformationalVersion() =>
        Header(PlainHost, FakeAssembly.Named("Supabase", "8.14.0-rc.1+9eb9fb35591296d2")).Should().StartWith("supabase-csharp/8.14.0-rc.1; ",
            "the version is the release version that sends the request");

    [TestMethod]
    public void HeaderValue_ShouldUseTheAssemblyVersion_GivenNoInformationalVersion() =>
        Header(PlainHost, FakeAssembly.Named("Supabase", version: new Version(1, 2, 3, 4))).Should().StartWith("supabase-csharp/1.2.3; ");

    [TestMethod]
    public void HeaderValue_ShouldUseZeroVersion_GivenNoVersionAtAll() =>
        Header(PlainHost, FakeAssembly.Named("Supabase")).Should().StartWith("supabase-csharp/0.0.0; ");

    [TestMethod]
    public void HeaderValue_ShouldReportBrowserWithoutPlatformVersion_GivenBrowserOsDescription() =>
        Header(PlainHost with { OsDescription = "Browser" }).Should().Contain("; platform=browser; platform-architecture=")
            .And.NotContain("platform-version", "a browser has no platform version, as supabase-js reports it");

    [TestMethod]
    [DataRow("WINDOWS", "Windows")]
    [DataRow("OSX", "macOS")]
    [DataRow("IOS", "iOS")]
    [DataRow("LINUX", "Linux")]
    [DataRow("ANDROID", "Android")]
    [DataRow("FREEBSD", "FreeBSD")]
    public void HeaderValue_ShouldUseTheRegisteredPlatformName_GivenOsPlatform(string osPlatform, string expected) =>
        Header(PlainHost with { IsOsPlatform = OnlyPlatform(OSPlatform.Create(osPlatform)) })
            .Should().Contain($"; platform={expected}; platform-version={OsVersion};", "the spec's Platform names table fixes name and casing");

    [TestMethod]
    public void HeaderValue_ShouldUseTheReportedOsName_GivenUnregisteredPlatform() =>
        Header(PlainHost with { OsDescription = " Solaris 11.4" }).Should().Contain("; platform=Solaris; ",
            "any other platform is sent with the name the runtime reports for it");

    [TestMethod]
    [DataRow("X86", "386")]
    [DataRow("X64", "amd64")]
    [DataRow("Arm", "arm")]
    [DataRow("Armv6", "arm")]
    [DataRow("Arm64", "arm64")]
    [DataRow("Wasm", "wasm")]
    [DataRow("S390x", "s390x")]
    [DataRow("LoongArch64", "loong64")]
    [DataRow("Ppc64le", "ppc64le")]
    [DataRow("RiscV64", "riscv64")]
    public void HeaderValue_ShouldUseTheGoArchitectureName_GivenOsArchitecture(string osArchitecture, string expected) =>
        Header(PlainHost with { OsArchitecture = osArchitecture }).Should().Contain($"; platform-architecture={expected}; ",
            "the architecture uses Go's GOARCH names (client_info spec, Parameters)");

    [TestMethod]
    public void HeaderValue_ShouldLeaveOutTheArchitecture_GivenArchitectureWithoutGoName() =>
        Header(PlainHost with { OsArchitecture = "Mips" }).Should().NotContain("platform-architecture");

    [TestMethod]
    public void HeaderValue_ShouldLeaveOutThePlatformVersion_GivenNoOsVersion() =>
        Header(PlainHost with { OsVersion = " " }).Should().NotContain("platform-version");

    [TestMethod]
    public void HeaderValue_ShouldReportNoFramework_GivenNoKnownFrameworkAssembly() =>
        Header(PlainHost).Should().NotContain("framework", "the SDK never sends a placeholder such as unknown");

    [TestMethod]
    public void HeaderValue_ShouldReportMauiWithItsReleaseVersion_GivenMauiAssembly() =>
        Header(PlainHost with { LoadedAssemblies = [FakeAssembly.Named("Microsoft.Maui", "9.0.10+sha.4f1e0d1")] })
            .Should().EndWith("; framework=maui; framework-version=9.0.10");

    [TestMethod]
    public void HeaderValue_ShouldReportBlazorWithItsReleaseVersion_GivenBlazorAssembly() =>
        Header(PlainHost with { LoadedAssemblies = [FakeAssembly.Named("Microsoft.AspNetCore.Components", "10.0.0+d3aba8fe")] })
            .Should().EndWith("; framework=blazor; framework-version=10.0.0");

    [TestMethod]
    public void HeaderValue_ShouldPreferMauiOverBlazor_GivenBothAssemblies() =>
        Header(PlainHost with { LoadedAssemblies = [FakeAssembly.Named("Microsoft.AspNetCore.Components"), FakeAssembly.Named("Microsoft.Maui")] })
            .Should().EndWith("; framework=maui", "MAUI wins over Blazor in hybrid apps where both are present");

    [TestMethod]
    public void HeaderValue_ShouldReportUnityWithItsVersion_GivenUnityAssembly() =>
        Header(UnityHost with { UnityVersion = () => "6000.3.21f1" }).Should().EndWith("; framework=unity; framework-version=6000.3.21f1");

    [TestMethod]
    public void HeaderValue_ShouldReportUnityWithoutVersion_GivenUnityVersionIsUnavailable() =>
        Header(UnityHost).Should().EndWith("; framework=unity", "a stripped Application.unityVersion resolves to no version");

    [TestMethod]
    public void HeaderValue_ShouldReportDotnetRuntime_GivenUnity() =>
        Header(UnityHost).Should().Contain($"; runtime=dotnet; runtime-version={RuntimeVersion}; ",
            "runtime names the language runtime, never the host (client_info spec, Parameters)");

    [TestMethod]
    [DataRow("Android", "Android")]
    [DataRow("IPhonePlayer", "iOS")]
    [DataRow("WindowsPlayer", "Windows")]
    [DataRow("WSAPlayerARM", "Windows")]
    [DataRow("OSXEditor", "macOS")]
    [DataRow("LinuxServer", "Linux")]
    [DataRow("EmbeddedLinuxArm64", "Linux")]
    [DataRow("Switch", "Switch")]
    public void HeaderValue_ShouldReportUnityPlatform_GivenUnityRuntimePlatform(string runtimePlatform, string expected) =>
        Header(UnityHost with { OsDescription = "Unix 5.15.189.13", UnityPlatform = () => runtimePlatform })
            .Should().Contain($"; platform={expected}; platform-version={OsVersion};");

    [TestMethod]
    public void HeaderValue_ShouldReportBrowserWithoutPlatformVersion_GivenUnityWebGl() =>
        Header(UnityHost with { UnityPlatform = () => "WebGLPlayer" }).Should().Contain("; platform=browser; platform-architecture=");

    [TestMethod]
    public void HeaderValue_ShouldFallBackToOsPlatform_GivenUnityPlatformIsUnavailable() =>
        Header(UnityHost with { OsDescription = "Unix 5.15.189.13" }).Should().Contain("; platform=Unix; ");

    [TestMethod]
    public void HeaderValue_ShouldIgnoreUnityPlatform_GivenNoUnityAssembly() =>
        Header(PlainHost with { OsDescription = "Solaris 11", UnityPlatform = () => "Android" }).Should().Contain("; platform=Solaris; ");

    [TestMethod]
    [DataRow("Switch;2", "Switch%3B2")]
    [DataRow("Café", "Caf%C3%A9")]
    [DataRow("100%", "100%25")]
    [DataRow("Tab\tStop", "Tab%09Stop")]
    [DataRow("Del\u007F", "Del%7F")]
    [DataRow("Space Station ~", "Space Station ~")]
    public void HeaderValue_ShouldPercentEncodeTheValue_GivenReservedOrNonAsciiCharacters(string reported, string expected) =>
        Header(UnityHost with { UnityPlatform = () => reported }).Should().Contain($"; platform={expected}; ",
            "outside printable ASCII, and for ; and %, a value is percent-encoded as its UTF-8 bytes; spaces are sent as they are");

    [TestMethod]
    public void HeaderValue_ShouldTrimTheValue_GivenSurroundingWhitespace() =>
        Header(UnityHost with { UnityVersion = () => " 6000.3 " }).Should().EndWith("; framework-version=6000.3");

    private static string Header(HostEnvironment host) => Header(host, Sdk);

    private static string Header(HostEnvironment host, Assembly clientAssembly) => ClientInfo.Of(clientAssembly, host).HeaderValue;

    private static Func<OSPlatform, bool> OnlyPlatform(OSPlatform platform) => candidate => candidate == platform;
}
