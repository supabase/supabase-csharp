using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Core.Tests.TestDoubles;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core;

namespace Core.Tests;

/// <summary>
/// Covers <see cref="Util.GetAssemblyVersion(Type)"/>, the <c>X-Client-Info</c> header. The public
/// entry point is asserted against the real host; the platform and framework branches are driven
/// through the internal seam that takes the ambient OS/assembly probes as parameters, so every OS and
/// framework arm is reachable without running on that OS or loading that framework.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UtilTests
{
    private static readonly HostEnvironment PlainHost = new(
        OsDescription: "any",
        IsOsPlatform: _ => false,
        LoadedAssemblies: [],
        FrameworkDescription: ".NET 10.0.0",
        UnityVersion: () => null,
        UnityPlatform: () => null);

    private static readonly HostEnvironment UnityHost = PlainHost with { LoadedAssemblies = [FakeAssembly.Named("UnityEngine.CoreModule")] };

    [TestMethod]
    public void GetAssemblyVersion_ShouldStartWithClientNameAndVersion() =>
        Util.GetAssemblyVersion(typeof(Util)).Should().MatchRegex(@"^supabase\.core-csharp/\S+");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportDotnetRuntime() =>
        Util.GetAssemblyVersion(typeof(Util)).Should().Contain("; runtime=dotnet");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportRuntimeVersion() =>
        Util.GetAssemblyVersion(typeof(Util)).Should().MatchRegex(@"; runtime-version=\S+");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportPlatformVersion() =>
        Util.GetAssemblyVersion(typeof(Util)).Should().MatchRegex(@"; platform-version=\S+");

    [TestMethod]
    public void GetAssemblyVersion_ShouldNotEmitEmptyValues()
    {
        var result = Util.GetAssemblyVersion(typeof(Util));
        using (new AssertionScope())
        {
            result.Should().NotMatchRegex(@"=\s*;", "every metadata key must carry a value");
            result.Should().NotMatchRegex(@"=$", "the header must not end on an empty value");
        }
    }

    [TestMethod]
    public void GetAssemblyVersion_ShouldReuseCachedValue_GivenAssemblyIsAlreadyGenerated()
    {
        Util.GetAssemblyVersion(typeof(Util));
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        Util.GetAssemblyVersion(typeof(Util));
        GC.GetAllocatedBytesForCurrentThread().Should().Be(allocated);
    }

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportBrowserPlatform_GivenBrowserOsDescription() =>
        HeaderFor("Browser", PlainHost.IsOsPlatform).Should().Contain("; platform=browser;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportWindowsPlatform_GivenWindows() =>
        HeaderFor("any", OnlyPlatform(OSPlatform.Windows)).Should().Contain("; platform=Windows;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportMacOSPlatform_GivenOSX() =>
        HeaderFor("any", OnlyPlatform(OSPlatform.OSX)).Should().Contain("; platform=macOS;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportIOSPlatform_GivenIOS() =>
        HeaderFor("any", OnlyPlatform(OSPlatform.Create("iOS"))).Should().Contain("; platform=iOS;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportLinuxPlatform_GivenLinux() =>
        HeaderFor("any", OnlyPlatform(OSPlatform.Linux)).Should().Contain("; platform=Linux;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportAndroidPlatform_GivenAndroid() =>
        HeaderFor("any", OnlyPlatform(OSPlatform.Create("Android"))).Should().Contain("; platform=Android;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldFallBackToOsDescription_GivenUnrecognizedPlatform() =>
        HeaderFor("Solaris 11", PlainHost.IsOsPlatform).Should().Contain("; platform=Solaris 11;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportUnknownFramework_GivenNoKnownFrameworkAssembly() =>
        Header(PlainHost).Should().Contain("; framework=unknown");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportMauiFramework_GivenMauiAssembly() =>
        HeaderWith(FakeAssembly.Named("Microsoft.Maui")).Should().Contain("; framework=maui");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportBlazorFramework_GivenBlazorAssembly() =>
        HeaderWith(FakeAssembly.Named("Microsoft.AspNetCore.Components")).Should().Contain("; framework=blazor");

    [TestMethod]
    public void GetAssemblyVersion_ShouldPreferMauiOverBlazor_GivenBothAssemblies() =>
        HeaderWith(FakeAssembly.Named("Microsoft.AspNetCore.Components"), FakeAssembly.Named("Microsoft.Maui"))
            .Should().Contain("; framework=maui", "MAUI wins over Blazor in hybrid apps where both are present");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportUnityFrameworkWithVersion_GivenUnityAssembly() =>
        Header(UnityHost with { UnityVersion = () => "6000.3.21f1" })
            .Should().Contain("; framework=unity; framework-version=6000.3.21f1");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportUnityFrameworkWithoutVersion_GivenUnityVersionIsUnavailable() =>
        Header(UnityHost)
            .Should().Contain("; framework=unity").And.NotContain("framework-version",
                "a stripped Application.unityVersion must resolve to no version, not throw");

    [TestMethod]
    [DataRow("Android", "Android")]
    [DataRow("IPhonePlayer", "iOS")]
    [DataRow("WebGLPlayer", "browser")]
    [DataRow("WindowsPlayer", "Windows")]
    [DataRow("WSAPlayerARM", "Windows")]
    [DataRow("OSXEditor", "macOS")]
    [DataRow("LinuxServer", "Linux")]
    [DataRow("EmbeddedLinuxArm64", "Linux")]
    [DataRow("Switch", "Switch")]
    public void GetAssemblyVersion_ShouldReportUnityPlatform_GivenUnityRuntimePlatform(string runtimePlatform, string expected) =>
        Header(UnityHost with { OsDescription = "Unix 5.15.189.13", UnityPlatform = () => runtimePlatform })
            .Should().Contain($"; platform={expected};");

    [TestMethod]
    public void GetAssemblyVersion_ShouldFallBackToOsPlatform_GivenUnityPlatformIsUnavailable() =>
        Header(UnityHost with { OsDescription = "Unix 5.15.189.13" })
            .Should().Contain("; platform=Unix 5.15.189.13;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldIgnoreUnityPlatform_GivenNoUnityAssembly() =>
        Header(PlainHost with { OsDescription = "Solaris 11", UnityPlatform = () => "Android" })
            .Should().Contain("; platform=Solaris 11;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportMonoRuntimeVersion_GivenUnityOnMono() =>
        Header(UnityHost with { FrameworkDescription = "Mono 6.13.0 (Visual Studio built mono)" })
            .Should().Contain("; runtime=mono; runtime-version=6.13.0;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportIl2CppRuntimeWithoutVersion_GivenUnityOnIl2Cpp() =>
        Header(UnityHost with { FrameworkDescription = "Mono Unity IL2CPP(Oct  1 2025 10:00:00)" })
            .Should().Contain("; runtime=il2cpp; framework=unity");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportDotnetRuntime_GivenUnityWithUnrecognizedFrameworkDescription() =>
        Header(UnityHost with { FrameworkDescription = ".NET 10.0.0" })
            .Should().MatchRegex("; runtime=dotnet; runtime-version=[^;]+; framework=unity");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportDotnetRuntime_GivenMonoOutsideUnity() =>
        Header(PlainHost with { FrameworkDescription = "Mono 6.13.0 (Visual Studio built mono)" })
            .Should().Contain("; runtime=dotnet;");

    private static string Header(HostEnvironment host) => Util.GetAssemblyVersion(typeof(Util), host);

    private static string HeaderFor(string osDescription, Func<OSPlatform, bool> isOsPlatform) =>
        Header(PlainHost with { OsDescription = osDescription, IsOsPlatform = isOsPlatform });

    private static string HeaderWith(params Assembly[] loadedAssemblies) =>
        Header(PlainHost with { LoadedAssemblies = loadedAssemblies });

    private static Func<OSPlatform, bool> OnlyPlatform(OSPlatform platform) => p => p == platform;
}
