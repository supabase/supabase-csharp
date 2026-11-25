using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.NamespaceExists;

namespace Storage.Tests.NamespaceExists;

/// <summary>
/// Unit tests for building the identifier of a namespace: which levels it refuses and how two identifiers compare.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class IcebergNamespaceTests
{
    [TestMethod]
    public void Of_ShouldKeepLevelsInOrder() =>
        IcebergNamespace.Of("sales", "europe").Levels.Should().Equal("sales", "europe");

    [TestMethod]
    public void Of_ShouldNotFollowLaterChangesToTheArray()
    {
        var levels = new[] { "sales" };
        var sut = IcebergNamespace.Of(levels);
        levels[0] = "changed";
        sut.Levels.Should().Equal("sales");
    }

    [TestMethod]
    public void Of_ShouldThrowArgumentNullException_GivenNullLevels() =>
        FluentActions.Invoking(() => IcebergNamespace.Of(null!))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("levels");

    [TestMethod]
    public void Of_ShouldThrowArgumentException_GivenNoLevel() =>
        FluentActions.Invoking(() => IcebergNamespace.Of())
            .Should().Throw<ArgumentException>("a namespace is made of at least one level")
            .WithMessage("A namespace needs at least one level.*")
            .Which.ParamName.Should().Be("levels");

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Of_ShouldThrowArgumentException_GivenBlankLevel(string? level) =>
        FluentActions.Invoking(() => IcebergNamespace.Of("sales", level!))
            .Should().Throw<ArgumentException>("a blank level names nothing")
            .WithMessage("A namespace level must be provided.*")
            .Which.ParamName.Should().Be("levels");

    [TestMethod]
    public void Of_ShouldThrowArgumentException_GivenLevelContainingTheUnitSeparator() =>
        FluentActions.Invoking(() => IcebergNamespace.Of("a\u001Fb"))
            .Should().Throw<ArgumentException>("the separator would split the level in two on the wire")
            .WithMessage("A namespace level cannot contain the unit separator (U+001F).*")
            .Which.ParamName.Should().Be("levels");

    [TestMethod]
    public void Equals_ShouldBeTrue_GivenSameLevels() =>
        IcebergNamespace.Of("sales", "europe").Should().Be(IcebergNamespace.Of("sales", "europe"));

    [TestMethod]
    public void Equals_ShouldBeFalse_GivenOtherLevels() =>
        IcebergNamespace.Of("sales", "europe").Should().NotBe(IcebergNamespace.Of("sales", "asia"));

    [TestMethod]
    public void Equals_ShouldBeFalse_GivenFewerLevels() =>
        IcebergNamespace.Of("sales", "europe").Should().NotBe(IcebergNamespace.Of("sales"));

    [TestMethod]
    public void GetHashCode_ShouldMatch_GivenSameLevels() =>
        new HashSet<IcebergNamespace> { IcebergNamespace.Of("sales", "europe") }
            .Should().Contain(IcebergNamespace.Of("sales", "europe"));

    [TestMethod]
    public void GetHashCode_ShouldDiffer_GivenOtherLevels() =>
        IcebergNamespace.Of("sales", "europe").GetHashCode().Should().NotBe(IcebergNamespace.Of("sales", "asia").GetHashCode());

    [TestMethod]
    public void ToString_ShouldJoinLevelsWithDots() =>
        IcebergNamespace.Of("sales", "europe").ToString().Should().Be("sales.europe");
}
