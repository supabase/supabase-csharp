using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage.AnalyticsBuckets.DeleteBucket;

namespace Storage.Tests.AnalyticsBuckets.DeleteBucket;

/// <summary>
/// Unit tests for the checks made before the wire when deleting an analytics bucket. The name format
/// itself is validated by the service; the SDK only refuses names it cannot send as one path segment.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeleteAnalyticsBucketPathTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(".")]
    [DataRow("..")]
    public void For_ShouldThrowArgumentException_GivenUnusableName(string? name) =>
        FluentActions.Invoking(() => DeleteAnalyticsBucketPath.For("http://h/iceberg", name!))
            .Should().Throw<ArgumentException>("a null, empty or dot-segment name would reach another route")
            .WithMessage("An analytics bucket name*");

    [TestMethod]
    public void For_ShouldAppendEscapedNameToBucketRoute() =>
        DeleteAnalyticsBucketPath.For("http://h/iceberg", "my bucket").Should().Be("http://h/iceberg/bucket/my%20bucket");

    [TestMethod]
    public void For_ShouldAcceptNamesContainingDots() =>
        DeleteAnalyticsBucketPath.For("http://h/iceberg", "a..b").Should().Be("http://h/iceberg/bucket/a..b");
}
