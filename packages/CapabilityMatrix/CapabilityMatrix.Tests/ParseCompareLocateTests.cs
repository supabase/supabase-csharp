using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests;

[TestClass]
public class ComplianceYamlTests
{
    [TestMethod]
    public void ParsesScalarAndMapEntries()
    {
        var compliance = ComplianceYaml.Parse(
            """
            sdk: csharp
            features:
              a.simple: implemented
              a.rich:
                status: partially_implemented
                no_public_symbols: true
                note: "missing bits"
                symbols:
                  - Z.Last
                  - A.First
            """);

        compliance.Sdk.Should().Be("csharp");
        compliance.Features["a.simple"].Should().BeEquivalentTo(
            new NormalizedEntry("implemented", null, [], false));
        var rich = compliance.Features["a.rich"];
        rich.Status.Should().Be("partially_implemented");
        rich.Note.Should().Be("missing bits");
        rich.NoPublicSymbols.Should().BeTrue();
        rich.Symbols.Should().Equal("A.First", "Z.Last");
    }

    [TestMethod]
    public void ProjectsEntriesIntoTheSameNormalizedShape()
    {
        var normalized = ComplianceYaml.FromEntries(
            [new ComplianceEntry("a.b", CapabilityStatus.PartiallyImplemented, "", ["B", "A"], true)]);

        normalized.Sdk.Should().Be("csharp");
        var entry = normalized.Features["a.b"];
        entry.Status.Should().Be("partially_implemented");
        entry.Note.Should().BeNull("an empty note normalizes to null");
        entry.NoPublicSymbols.Should().BeTrue();
        entry.Symbols.Should().Equal("A", "B");
    }
}

[TestClass]
public class ComplianceComparerTests
{
    private static NormalizedCompliance One(string key, NormalizedEntry entry) =>
        new("csharp", new Dictionary<string, NormalizedEntry>(StringComparer.Ordinal) { [key] = entry });

    [TestMethod]
    public void ReportsNoDifferencesForEquivalentModels()
    {
        var a = One("k", new NormalizedEntry("implemented", "n", ["A"], false));
        var b = One("k", new NormalizedEntry("implemented", "n", ["A"], false));
        ComplianceComparer.Diff(a, b).Should().BeEmpty();
    }

    [TestMethod]
    public void ReportsEveryKindOfMismatch()
    {
        var expected = new NormalizedCompliance("csharp", new Dictionary<string, NormalizedEntry>(StringComparer.Ordinal)
        {
            ["only.expected"] = new("implemented", null, [], false),
            ["shared"] = new("implemented", "old", ["Keep", "Gone"], false),
        });
        var actual = new NormalizedCompliance("dart", new Dictionary<string, NormalizedEntry>(StringComparer.Ordinal)
        {
            ["only.actual"] = new("implemented", null, [], false),
            ["shared"] = new("partially_implemented", "new", ["Keep", "Added"], true),
        });

        var diff = ComplianceComparer.Diff(expected, actual);

        diff.Should().Contain(d => d.Contains("sdk:"));
        diff.Should().Contain(d => d.Contains("only.expected") && d.Contains("missing from generated"));
        diff.Should().Contain(d => d.Contains("only.actual") && d.Contains("not in expected"));
        diff.Should().Contain(d => d.Contains("shared: status"));
        diff.Should().Contain(d => d.Contains("shared: note"));
        diff.Should().Contain(d => d.Contains("shared: no_public_symbols"));
        diff.Should().Contain(d => d.Contains("shared: symbols") && d.Contains("missing: Gone") && d.Contains("extra: Added"));
    }
}

[TestClass]
public class LocatorTests
{
    private static DeclaredSymbol Decl(string name, string file) => new(name, "method", file, 7, true, []);

    [TestMethod]
    public void ClassifiesResolvedAmbiguousAndUnresolvedSymbols()
    {
        var compliance = new NormalizedCompliance("csharp", new Dictionary<string, NormalizedEntry>(StringComparer.Ordinal)
        {
            ["f.one"] = new("implemented", null, ["Client.A"], false),
            ["f.two"] = new("implemented", null, ["Client.B"], false),
            ["f.three"] = new("implemented", null, ["Client.Missing"], false),
        });
        var symbols = new List<DeclaredSymbol>
        {
            Decl("Client.A", "One.cs"),
            Decl("Client.B", "Two.cs"),
            Decl("Client.B", "Three.cs"),
        };

        var report = Locator.Locate(compliance, symbols);

        report.Resolved.Should().ContainSingle().Which.Symbol.Should().Be("Client.A");
        report.Ambiguous.Should().ContainSingle().Which.Candidates.Should().HaveCount(2);
        report.Unresolved.Should().ContainSingle().Which.Symbol.Should().Be("Client.Missing");
    }
}

[TestClass]
public class SourceDiscoveryTests
{
    [TestMethod]
    [DataRow("packages/Gotrue/Gotrue/Client.cs", true)]
    [DataRow("packages/Storage/Storage/Sub/Dir/File.cs", true)]
    [DataRow("packages/Gotrue/Gotrue/bin/Debug/Client.cs", false)]
    [DataRow("packages/Gotrue/Gotrue/obj/Client.g.cs", false)]
    [DataRow("packages/Gotrue/Gotrue.Tests/ClientTests.cs", false)]
    [DataRow("packages/CapabilityMatrix/CapabilityMatrix/Program.cs", false)]
    [DataRow("packages/Gotrue/Gotrue/README.md", false)]
    [DataRow("scripts/tool.cs", false)]
    public void IncludesOnlyProductionSdkSource(string path, bool expected) =>
        SourceDiscovery.IsSdkSourceFile(path).Should().Be(expected);
}

[TestClass]
public class CapabilityStatusTests
{
    [TestMethod]
    public void MapsStatusesToMatrixSpellings()
    {
        CapabilityStatus.Implemented.ToYamlValue().Should().Be("implemented");
        CapabilityStatus.PartiallyImplemented.ToYamlValue().Should().Be("partially_implemented");
    }
}
