using System;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core;

namespace Core.Tests;

/// <summary>
/// Covers <see cref="Util.GetAssemblyVersion(Type)"/>, the SDK's <c>X-Client-Info</c> value, against the real host.
/// Every platform, architecture and framework branch is pinned hermetically in <c>ClientIdentification/ClientInfoTests</c>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class UtilTests
{
    [TestMethod]
    public void GetAssemblyVersion_ShouldStartWithKebabCaseNameAndReleaseVersion() =>
        Util.GetAssemblyVersion(typeof(Util)).Should().MatchRegex(@"^supabase-core-csharp/[0-9A-Za-z.\-]+(; |$)",
            "the product token is the lowercase kebab-case name and the release version without build metadata (client_info spec, Format)");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportDotnetRuntimeWithItsVersion() =>
        Util.GetAssemblyVersion(typeof(Util)).Should().MatchRegex(@"; runtime=dotnet; runtime-version=[^;\s]+");

    [TestMethod]
    public void GetAssemblyVersion_ShouldReportPlatformWithItsVersionAndArchitecture() =>
        Util.GetAssemblyVersion(typeof(Util)).Should().MatchRegex(@"; platform=[^;]+; platform-version=[^;\s]+; platform-architecture=[a-z0-9]+;");

    [TestMethod]
    public void GetAssemblyVersion_ShouldNotEmitPlaceholdersOrEmptyValues()
    {
        var result = Util.GetAssemblyVersion(typeof(Util));
        using (new AssertionScope())
        {
            result.Should().NotContain("unknown", "a parameter the SDK cannot determine is left out (client_info spec, Format)");
            result.Should().NotMatchRegex(@"=\s*(;|$)", "every parameter carries a non-empty value");
        }
    }

    [TestMethod]
    public void GetAssemblyVersion_ShouldReuseCachedValue_GivenAssemblyIsAlreadyGenerated()
    {
        Util.GetAssemblyVersion(typeof(Util));
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        Util.GetAssemblyVersion(typeof(Util));
        GC.GetAllocatedBytesForCurrentThread().Should().Be(allocated, "the value is stamped on every request, so it is built once per client assembly (#523)");
    }
}
