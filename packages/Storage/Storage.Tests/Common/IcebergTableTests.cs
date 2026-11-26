using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.Common;

namespace Storage.Tests.Common;

/// <summary>
/// Unit tests for building the identifier of a table: which arguments it refuses, how it reads and how two
/// identifiers compare. The name itself is validated by the service.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class IcebergTableTests
{
    [TestMethod]
    public void Of_ShouldKeepNamespaceAndName() =>
        IcebergTable.Of(IcebergNamespace.Of("sales"), "events").Should().Match<IcebergTable>(table =>
            table.Namespace == IcebergNamespace.Of("sales") && table.Name == "events");

    [TestMethod]
    public void Of_ShouldThrowArgumentNullException_GivenNoNamespace() =>
        FluentActions.Invoking(() => IcebergTable.Of(null!, "events"))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("namespace");

    [TestMethod]
    public void Of_ShouldThrowArgumentNullException_GivenNoName() =>
        FluentActions.Invoking(() => IcebergTable.Of(IcebergNamespace.Of("sales"), null!))
            .Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("name");

    [TestMethod]
    public void Of_ShouldAcceptBlankName() =>
        IcebergTable.Of(IcebergNamespace.Of("sales"), " ").Name.Should().Be(" ", "the service, not the SDK, judges the name");

    [TestMethod]
    public void ToString_ShouldJoinNamespaceAndNameWithDots() =>
        IcebergTable.Of(IcebergNamespace.Of("sales", "europe"), "events").ToString().Should().Be("sales.europe.events");

    [TestMethod]
    public void Equals_ShouldBeTrue_GivenSameNamespaceAndName() =>
        IcebergTable.Of(IcebergNamespace.Of("sales"), "events").Should().Be(IcebergTable.Of(IcebergNamespace.Of("sales"), "events"));

    [TestMethod]
    public void Equals_ShouldBeFalse_GivenDifferentName() =>
        IcebergTable.Of(IcebergNamespace.Of("sales"), "events").Should().NotBe(IcebergTable.Of(IcebergNamespace.Of("sales"), "orders"));
}
