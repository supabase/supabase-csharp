using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.AnalyticsBuckets.Iceberg.DeleteTable;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.DeleteTable;

/// <summary>
/// Unit tests for the checks made before the wire when deleting a table, and for the defaults of its options.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeleteTableTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task DeleteTableAsync_ShouldThrowArgumentNullException_GivenNoTable() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").DeleteTableAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no table to delete"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task DeleteTableAsync_ShouldThrowArgumentException_GivenBlankName(string name) =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").DeleteTableAsync(IcebergNamespace.Of("sales").Table(name)))
            .Should().ThrowAsync<ArgumentException>("a blank name would reach the route of the namespace's tables")
            .WithMessage("A table name must be provided.*"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public async Task DeleteTableAsync_ShouldThrowArgumentException_GivenRelativeSegmentName(string name) =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").DeleteTableAsync(IcebergNamespace.Of("sales").Table(name)))
            .Should().ThrowAsync<ArgumentException>("a dot-segment name would reach another route, such as the namespace itself")
            .WithMessage("A table name cannot be a relative path segment.*"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    public void DeleteTableOptions_ShouldNotSetPurge_GivenNoneSet() =>
        new DeleteTableOptions().Purge.Should().BeNull("the service owns the default of purgeRequested");
}
