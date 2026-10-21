using FluentAssertions;
using Supabase.CapabilityMatrix.Capabilities;
using Supabase.CapabilityMatrix.GeneratingTheComplianceFile;

namespace Supabase.CapabilityMatrix.Tests.GeneratingTheComplianceFile;

/// <summary>Rendering the consolidated features into the sdk-compliance.yaml text.</summary>
[TestClass]
[TestCategory("Unit")]
public class ComplianceFileTests
{
    private const string AuthenticationBanner = "  # ── Authentication ";

    private static FeatureCompliance Feature(
        string key, CapabilityStatus status, string? note, bool noPublicSymbols, params string[] symbols) =>
        new(key, status, note, symbols, noPublicSymbols);

    private static string Render(params FeatureCompliance[] features) =>
        ComplianceFile.For(features, Fixtures.AuthCatalogue());

    [TestMethod]
    public void For_ShouldStartWithTheSdkHeader() =>
        Render().Should().StartWith("sdk: csharp\nfeatures:\n");

    [TestMethod]
    public void For_ShouldPadTheAreaBannerToAFixedWidth() =>
        Render(Feature("auth.sign_in.sign_up", CapabilityStatus.Implemented, null, false, "Client.SignUp"))
            .Split('\n').Single(line => line.Contains("Authentication"))
            .Should().Be(AuthenticationBanner + new string('─', 79 - AuthenticationBanner.Length));

    [TestMethod]
    public void For_ShouldLayFeaturesOutInCatalogueOrderAndOmitAbsentOnes() =>
        FeatureKeysIn(Render(
            Feature("auth.session.get_user", CapabilityStatus.Implemented, null, false, "Client.GetUser"),
            Feature("auth.sign_in.sign_up", CapabilityStatus.Implemented, null, false, "Client.SignUp")))
            .Should().Equal("auth.sign_in.sign_up", "auth.session.get_user");

    [TestMethod]
    public void For_ShouldEscapeQuotes_GivenANoteWithQuotes() =>
        Render(Feature("auth.sign_in.sign_up", CapabilityStatus.Partial, "the \"col\" form", false, "Client.SignUp"))
            .Should().Contain("""    note: "the \"col\" form""");

    [TestMethod]
    public void For_ShouldRecordNoPublicSymbolsInsteadOfASymbolList() =>
        Render(Feature("auth.sign_in.sign_up", CapabilityStatus.Implemented, "via callback", noPublicSymbols: true))
            .Should().Contain("    no_public_symbols: true\n").And.NotContain("    symbols:");

    private static IEnumerable<string> FeatureKeysIn(string yaml) =>
        yaml.Split('\n')
            .Where(line => line.StartsWith("  auth.", StringComparison.Ordinal))
            .Select(line => line.Trim().TrimEnd(':'));
}
