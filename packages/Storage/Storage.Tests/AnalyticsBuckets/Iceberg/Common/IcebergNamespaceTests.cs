using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// Unit tests for building the identifier of a namespace and of its tables: which arguments it refuses and how two
/// identifiers compare. Table names are validated by the service.
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

    [TestMethod]
    public void Path_ShouldEscapeAndJoinLevelsWithTheEncodedUnitSeparator() =>
        IcebergNamespace.Of("a b", "c/d").Path.Should().Be("namespaces/a%20b%1Fc%2Fd", "the Iceberg REST spec joins levels with the 0x1F separator");

    [TestMethod]
    public void TablesPath_ShouldNestTheTablesCollectionUnderTheNamespace() =>
        IcebergNamespace.Of("sales", "europe").TablesPath.Should().Be("namespaces/sales%1Feurope/tables", "the Iceberg REST spec lists and creates tables under the namespace");

    [TestMethod]
    public void Path_ShouldAcceptLevelsContainingDots() =>
        IcebergNamespace.Of("a..b").Path.Should().Be("namespaces/a..b");

    [TestMethod]
    public void Path_ShouldAcceptDotLevelsInAMultiLevelNamespace() =>
        IcebergNamespace.Of("..", "a").Path.Should().Be("namespaces/..%1Fa", "the joined segment is no longer a relative one");

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public void Path_ShouldThrowArgumentException_GivenRelativeSegmentNamespace(string level) =>
        FluentActions.Invoking(() => IcebergNamespace.Of(level).Path)
            .Should().Throw<ArgumentException>("a dot-segment namespace would reach another route")
            .WithMessage("A namespace cannot be a relative path segment.*")
            .Which.ParamName.Should().Be("namespace");

    [TestMethod]
    public void Table_ShouldKeepNamespaceAndName() =>
        IcebergNamespace.Of("sales").Table("events").Should().Match<IcebergTable>(table =>
            table.Namespace == IcebergNamespace.Of("sales") && table.Name == "events");

    [TestMethod]
    public void Table_ShouldThrowArgumentNullException_GivenNoName() =>
        FluentActions.Invoking(() => IcebergNamespace.Of("sales").Table(null!))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("name");

    [TestMethod]
    public void Table_ShouldAcceptBlankName() =>
        IcebergNamespace.Of("sales").Table(" ").Name.Should().Be(" ", "the service, not the SDK, judges the name");
}
