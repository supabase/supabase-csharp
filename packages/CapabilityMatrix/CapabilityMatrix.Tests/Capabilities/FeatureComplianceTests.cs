using FluentAssertions;
using Supabase.CapabilityMatrix.Capabilities;

namespace Supabase.CapabilityMatrix.Tests.Capabilities;

/// <summary>Folding the declarations scattered across members into one entry per feature.</summary>
[TestClass]
[TestCategory("Unit")]
public class FeatureComplianceTests
{
    private static DeclaredCapability Declared(
        string key, CapabilityStatus status, string symbol, bool fromPublicApi = true, string? note = null) =>
        new(key, status, note, symbol, fromPublicApi);

    [TestMethod]
    public void Consolidate_ShouldMergeTheSymbolsOfEveryDeclaringMember() =>
        FeatureCompliance.Consolidate(
        [
            Declared("auth.sign_in", CapabilityStatus.Implemented, "Client.SignInWithPassword"),
            Declared("auth.sign_in", CapabilityStatus.Implemented, "Client.SignIn"),
        ]).Single().Symbols.Should().Equal("Client.SignIn", "Client.SignInWithPassword");

    [TestMethod]
    public void Consolidate_ShouldReportPartial_GivenAnyMemberIsPartial() =>
        FeatureCompliance.Consolidate(
        [
            Declared("x.y", CapabilityStatus.Implemented, "A"),
            Declared("x.y", CapabilityStatus.Partial, "B", note: "gap"),
        ]).Single().Status.Should().Be(CapabilityStatus.Partial);

    [TestMethod]
    public void Consolidate_ShouldReportNoPublicSymbols_GivenOnlyNonPublicMembers() =>
        FeatureCompliance.Consolidate(
        [
            Declared("client.sync", CapabilityStatus.Implemented, "Client.GetAuthHeaders", fromPublicApi: false, note: "callback"),
        ]).Single().Should().BeEquivalentTo(new { NoPublicSymbols = true, Symbols = Array.Empty<string>() });
}
