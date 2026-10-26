using System;
using System.Collections.Generic;
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
    private static readonly Func<OSPlatform, bool> NoPlatform = _ => false;
    private static readonly IReadOnlyCollection<Assembly> NoFrameworks = [];

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
        HeaderFor("Browser", NoPlatform).Should().Contain("; platform=browser;");

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
        HeaderFor("Solaris 11", NoPlatform).Should().Contain("; platform=Solaris 11;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportUnknownFramework_GivenNoKnownFrameworkAssembly() =>
        HeaderWith(NoFrameworks).Should().Contain("; framework=unknown");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportMauiFramework_GivenMauiAssembly() =>
        HeaderWith([FakeAssembly.Named("Microsoft.Maui")]).Should().Contain("; framework=maui");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportBlazorFramework_GivenBlazorAssembly() =>
        HeaderWith([FakeAssembly.Named("Microsoft.AspNetCore.Components")]).Should().Contain("; framework=blazor");

    [TestMethod]
    public void GetAssemblyVersion_ShouldPreferMauiOverBlazor_GivenBothAssemblies() =>
        HeaderWith([FakeAssembly.Named("Microsoft.AspNetCore.Components"), FakeAssembly.Named("Microsoft.Maui")])
            .Should().Contain("; framework=maui", "MAUI wins over Blazor in hybrid apps where both are present");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportUnityFrameworkWithVersion_GivenUnityAssembly() =>
        HeaderWith([FakeAssembly.Named("UnityEngine.CoreModule"), typeof(UtilTests).Assembly])
            .Should().Contain("; framework=unity; framework-version=2022.3.5f1");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportUnityFrameworkWithoutVersion_GivenNoUnityVersionAttribute() =>
        HeaderWith([FakeAssembly.Named("UnityEngine.CoreModule")])
            .Should().Contain("; framework=unity").And.NotContain("framework-version",
                "an absent Unity version attribute must resolve to no version, not throw");

    private static string HeaderFor(string osDescription, Func<OSPlatform, bool> isOsPlatform) =>
        Util.GetAssemblyVersion(typeof(Util), osDescription, isOsPlatform, NoFrameworks);

    private static string HeaderWith(IReadOnlyCollection<Assembly> loadedAssemblies) =>
        Util.GetAssemblyVersion(typeof(Util), "any", NoPlatform, loadedAssemblies);

    private static Func<OSPlatform, bool> OnlyPlatform(OSPlatform platform) => p => p == platform;
}
