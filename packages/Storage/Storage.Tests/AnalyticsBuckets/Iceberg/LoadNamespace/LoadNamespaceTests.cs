using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.LoadNamespace;

/// <summary>
/// Unit tests for the checks made before the wire when loading a namespace.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class LoadNamespaceTests
{
    private readonly Client sut = new("http://localhost:1/storage/v1");

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldThrowArgumentNullException_GivenNoNamespace() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").LoadNamespaceAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>("there is no namespace to load"))
            .Which.ParamName.Should().Be("namespace");

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldThrowArgumentException_GivenRelativeSegmentNamespace() =>
        (await FluentActions.Awaiting(() => this.sut.Analytics.From("analytics-data").LoadNamespaceAsync(IcebergNamespace.Of("..")))
            .Should().ThrowAsync<ArgumentException>("a dot-segment level would reach another route"))
            .WithMessage("A namespace cannot be a relative path segment.*");
}
