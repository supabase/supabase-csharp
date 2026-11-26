using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.NamespaceExists;

/// <summary>
/// Unit tests for the checks made before the wire when scoping to a catalog and checking a namespace. The
/// names themselves are validated by the service; the SDK only refuses values it cannot send as one path segment.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class NamespaceExistsTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(".")]
    [DataRow("..")]
    public void From_ShouldThrowArgumentException_GivenUnusableBucketName(string? bucketName) =>
        FluentActions.Invoking(() => this.sut.Analytics.From(bucketName!))
            .Should().Throw<ArgumentException>("a null, empty or dot-segment name would reach another route")
            .WithMessage("An analytics bucket name*")
            .Which.ParamName.Should().Be("bucketName");

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldThrowArgumentNullException_GivenNoNamespace() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").NamespaceExistsAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no namespace to look for"))
            .Which.ParamName.Should().Be("namespace");

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    public async Task NamespaceExistsAsync_ShouldThrowArgumentException_GivenRelativeSegmentNamespace(string level) =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").NamespaceExistsAsync(IcebergNamespace.Of(level)))
            .Should().ThrowAsync<ArgumentException>("a dot-segment level would reach another route"))
            .WithMessage("A namespace cannot be a relative path segment.*");
}
