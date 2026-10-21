using FluentAssertions;

namespace Supabase.CapabilityMatrix.Tests.CanonicalMatrix;

/// <summary>Loading the canonical feature catalogue from the matrix spec documents.</summary>
[TestClass]
[TestCategory("Unit")]
public class FeatureCatalogueTests
{
    [TestMethod]
    public void Knows_ShouldReturnTrue_GivenAFeatureInTheSpec() =>
        Fixtures.AuthCatalogue().Knows("auth.sign_in.sign_up").Should().BeTrue();

    [TestMethod]
    public void Knows_ShouldReturnFalse_GivenAnInventedFeature() =>
        Fixtures.AuthCatalogue().Knows("auth.sign_in.telepathy").Should().BeFalse();

    [TestMethod]
    public void From_ShouldKeepGroupsInSpecOrder() =>
        Fixtures.AuthCatalogue().Areas.Single().Groups.Select(group => group.Id).Should().Equal("sign_in", "session");
}
