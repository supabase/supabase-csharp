using FluentAssertions;
using Supabase.CapabilityMatrix.Capabilities;
using Supabase.CapabilityMatrix.ValidatingDeclarations;

namespace Supabase.CapabilityMatrix.Tests.ValidatingDeclarations;

/// <summary>Reviewing the declared capabilities against the canonical catalogue.</summary>
[TestClass]
[TestCategory("Unit")]
public class DeclarationReviewTests
{
    private static DeclaredCapability Declared(
        string key, CapabilityStatus status = CapabilityStatus.Implemented, bool fromPublicApi = true, string? note = null) =>
        new(key, status, note, "Client.Member", fromPublicApi);

    private static IReadOnlyList<DeclarationProblem> Review(params DeclaredCapability[] declarations) =>
        DeclarationReview.Of(declarations, Fixtures.AuthCatalogue());

    [TestMethod]
    public void Of_ShouldReturnNoProblems_GivenKnownFeaturesDeclaredConsistently() =>
        Review(Declared("auth.sign_in.sign_up", CapabilityStatus.Partial, note: "gap")).Should().BeEmpty();

    [TestMethod]
    public void Of_ShouldFlagAFeatureKey_GivenTheMatrixDoesNotKnowIt() =>
        Review(Declared("auth.sign_in.telepathy")).Should().ContainSingle()
            .Which.Message.Should().Contain("auth.sign_in.telepathy");

    [TestMethod]
    public void Of_ShouldFlagAFeature_GivenItIsDeclaredWithTwoStatuses() =>
        Review(
            Declared("auth.sign_in.sign_up"),
            Declared("auth.sign_in.sign_up", CapabilityStatus.Partial, note: "gap"))
            .Should().ContainSingle().Which.Message.Should().Contain("one status");

    [TestMethod]
    public void Of_ShouldFlagANonPublicOnlyFeature_GivenItLacksANote() =>
        Review(Declared("auth.session.get_user", fromPublicApi: false)).Should().ContainSingle()
            .Which.Message.Should().Contain("note");
}
