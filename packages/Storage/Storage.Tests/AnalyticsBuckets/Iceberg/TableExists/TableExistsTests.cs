using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.TableExists;

/// <summary>
/// Unit tests for the checks made before the wire when checking that a table exists: a missing table, and a name that
/// would change the route.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class TableExistsTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task TableExistsAsync_ShouldThrowArgumentNullException_GivenNoTable() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").TableExistsAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no table to look for"))
            .Which.ParamName.Should().Be("table");

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public async Task TableExistsAsync_ShouldThrowArgumentException_GivenRelativeSegmentName(string name) =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").TableExistsAsync(IcebergNamespace.Of("sales").Table(name)))
            .Should().ThrowAsync<ArgumentException>("a dot-segment name would reach another route")
            .WithMessage("A table name cannot be a relative path segment.*"))
            .Which.ParamName.Should().Be("table");
}
