using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.LoadTable;

/// <summary>
/// Unit tests for the checks made before the wire when loading a table: a missing table, and a name that would
/// change the route.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class LoadTableTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task LoadTableAsync_ShouldThrowArgumentNullException_GivenNoTable() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").LoadTableAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no table to load"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task LoadTableAsync_ShouldThrowArgumentException_GivenBlankName(string name) =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").LoadTableAsync(IcebergNamespace.Of("sales").Table(name)))
            .Should().ThrowAsync<ArgumentException>("a blank name would reach the route that lists tables")
            .WithMessage("A table name must be provided.*"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public async Task LoadTableAsync_ShouldThrowArgumentException_GivenRelativeSegmentName(string name) =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").LoadTableAsync(IcebergNamespace.Of("sales").Table(name)))
            .Should().ThrowAsync<ArgumentException>("a dot-segment name would reach another route")
            .WithMessage("A table name cannot be a relative path segment.*"))
            .Which.ParamName.Should().Be("table");
}
