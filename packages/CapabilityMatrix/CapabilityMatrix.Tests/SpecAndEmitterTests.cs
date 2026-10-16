using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests;

[TestClass]
public class SpecModelTests
{
    private const string AuthArea =
        """
        area: auth
        title: Authentication
        groups:
          - id: sign_in
            title: Sign-in
          - id: session
            title: Session
        features:
          - id: auth.sign_in.b
            group: sign_in
          - id: auth.sign_in.a
            group: sign_in
          - id: auth.session.x
            group: session
          - id: auth.loose
        """;

    [TestMethod]
    public void CollectsKnownIdsAndPreservesGroupAndFeatureOrder()
    {
        var spec = SpecModel.Parse([AuthArea]);

        spec.KnownIds.Should().BeEquivalentTo(["auth.sign_in.b", "auth.sign_in.a", "auth.session.x", "auth.loose"]);
        var area = spec.Areas.Single();
        area.Title.Should().Be("Authentication");
        area.Groups.Select(g => g.Id).Should().Equal("sign_in", "session");
        area.Groups[0].FeatureIds.Should().Equal("auth.sign_in.b", "auth.sign_in.a");
        area.UngroupedFeatureIds.Should().Equal("auth.loose");
    }

    [TestMethod]
    public void OrdersAreasByIdAndSkipsEmptyDocuments()
    {
        var spec = SpecModel.Parse(
        [
            "area: storage\ntitle: Storage\nfeatures: []",
            "area: auth\ntitle: Auth\nfeatures: []",
            "# just a comment\n",
        ]);

        spec.Areas.Select(a => a.Id).Should().Equal("auth", "storage");
    }
}

[TestClass]
public class YamlEmitterTests
{
    private static SpecModel Spec() => SpecModel.Parse(
    [
        """
        area: auth
        title: Authentication
        groups:
          - id: sign_in
            title: Sign-in
          - id: session
            title: Session
        features:
          - id: auth.sign_in.a
            group: sign_in
          - id: auth.session.x
            group: session
        """,
    ]);

    [TestMethod]
    public void EmitsHeaderBannerAndEntry()
    {
        var yaml = YamlEmitter.Emit(
            [new ComplianceEntry("auth.sign_in.a", CapabilityStatus.Implemented, null, ["Client.A", "Client.B"])],
            Spec());

        yaml.Should().StartWith("sdk: csharp\nfeatures:\n");
        var banner = yaml.Split('\n').Single(l => l.Contains("Authentication"));
        banner.Should().Be("  # ── Authentication " + new string('─', 79 - "  # ── Authentication ".Length));
        banner.Length.Should().Be(79);
        yaml.Should().Contain("  auth.sign_in.a:\n    status: implemented\n    symbols:\n      - Client.A\n      - Client.B\n");
        yaml.Should().EndWith("\n");
    }

    [TestMethod]
    public void EscapesNotesAndEmitsNoPublicSymbolsWithoutASymbolsBlock()
    {
        var yaml = YamlEmitter.Emit(
            [new ComplianceEntry("auth.sign_in.a", CapabilityStatus.Implemented, "He said \"hi\" \\o/", [], NoPublicSymbols: true)],
            Spec());

        yaml.Should().Contain("    no_public_symbols: true\n");
        yaml.Should().Contain("    note: \"He said \\\"hi\\\" \\\\o/\"\n");
        yaml.Should().NotContain("    symbols:");
    }

    [TestMethod]
    public void SeparatesGroupsWithABlankLineAndSkipsAbsentFeatures()
    {
        var yaml = YamlEmitter.Emit(
        [
            new ComplianceEntry("auth.sign_in.a", CapabilityStatus.Implemented, null, ["A"]),
            new ComplianceEntry("auth.session.x", CapabilityStatus.Implemented, null, ["B"]),
        ], Spec());

        // A blank line separates the sign_in group block from the session group block.
        yaml.Should().Contain("      - A\n\n  auth.session.x:");
    }

    [TestMethod]
    public void OmitsAreasWithNoMatchingEntries()
    {
        var yaml = YamlEmitter.Emit([], Spec());
        yaml.Should().Be("sdk: csharp\nfeatures:\n");
    }
}
