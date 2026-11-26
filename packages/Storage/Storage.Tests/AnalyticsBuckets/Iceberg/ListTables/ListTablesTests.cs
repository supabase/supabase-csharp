using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.AnalyticsBuckets.Iceberg.ListTables;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.ListTables;

/// <summary>
/// Unit tests for the checks made before the wire when listing the tables of a namespace, and for the defaults of its
/// options.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ListTablesTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task ListTablesAsync_ShouldThrowArgumentNullException_GivenNoNamespace() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").ListTablesAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no namespace to list"))
            .Which.ParamName.Should().Be("namespace");

    [TestMethod]
    public async Task ListTablesAsync_ShouldThrowArgumentException_GivenRelativeSegmentNamespace() =>
        await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").ListTablesAsync(IcebergNamespace.Of("..")))
            .Should().ThrowAsync<ArgumentException>("a dot-segment namespace would reach another route")
            .WithMessage("A namespace cannot be a relative path segment.*");

    [TestMethod]
    public void ListTablesOptions_ShouldSendNothing_GivenNoneSet()
    {
        var options = new ListTablesOptions();
        using (new AssertionScope())
        {
            options.PageSize.Should().BeNull("the service owns the default page size");
            options.NextPageToken.Should().BeNull("the first page needs no token");
        }
    }
}
