using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.Common;

/// <summary>
/// Unit tests for the identifier of a table: how it reads, how two identifiers compare and the path it addresses.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class IcebergTableTests
{
    [TestMethod]
    public void ToString_ShouldJoinNamespaceAndNameWithDots() =>
        IcebergNamespace.Of("sales", "europe").Table("events").ToString().Should().Be("sales.europe.events");

    [TestMethod]
    public void Equals_ShouldBeTrue_GivenSameNamespaceAndName() =>
        IcebergNamespace.Of("sales").Table("events").Should().Be(IcebergNamespace.Of("sales").Table("events"));

    [TestMethod]
    public void Equals_ShouldBeFalse_GivenDifferentName() =>
        IcebergNamespace.Of("sales").Table("events").Should().NotBe(IcebergNamespace.Of("sales").Table("orders"));

    [TestMethod]
    public void CollectionPath_ShouldBeTheTablesCollectionOfItsNamespace() =>
        IcebergNamespace.Of("sales", "europe").Table("events").CollectionPath
            .Should().Be("namespaces/sales%1Feurope/tables", "a table is created by posting to its namespace's tables");

    [TestMethod]
    public void Path_ShouldNestTheEscapedNameUnderTheTablesOfItsNamespace() =>
        IcebergNamespace.Of("sales", "europe").Table("a/b?c#d").Path
            .Should().Be("namespaces/sales%1Feurope/tables/a%2Fb%3Fc%23d", "a name must not start a path segment, query or fragment");

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Path_ShouldThrowArgumentException_GivenBlankName(string name) =>
        FluentActions.Invoking(() => IcebergNamespace.Of("sales").Table(name).Path)
            .Should().Throw<ArgumentException>("a blank name would reach the route of the namespace's tables")
            .WithMessage("A table name must be provided.*")
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public void Path_ShouldThrowArgumentException_GivenRelativeSegmentName(string name) =>
        FluentActions.Invoking(() => IcebergNamespace.Of("sales").Table(name).Path)
            .Should().Throw<ArgumentException>("a dot-segment name would reach another route")
            .WithMessage("A table name cannot be a relative path segment.*")
            .Which.ParamName.Should().Be("table");
}
