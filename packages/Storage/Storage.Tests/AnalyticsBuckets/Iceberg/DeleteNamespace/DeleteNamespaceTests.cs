using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.DeleteNamespace;

/// <summary>
/// Unit tests for the checks made before the wire when deleting a namespace.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeleteNamespaceTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldThrowArgumentNullException_GivenNoNamespace() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").DeleteNamespaceAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no namespace to delete"))
            .Which.ParamName.Should().Be("namespace");

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldThrowArgumentException_GivenRelativeSegmentNamespace() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").DeleteNamespaceAsync(IcebergNamespace.Of("..")))
            .Should().ThrowAsync<ArgumentException>("a dot-segment level would reach another route"))
            .WithMessage("A namespace cannot be a relative path segment.*");
}
