using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.GetVectorIndex;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).GetIndexAsync</c> against the vector service of the local stack,
/// in the uniquely named bucket and index that <see cref="VectorIndexE2EFixture"/> seeds and removes.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorIndexDescriptionTests : VectorIndexE2EFixture
{
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    [TestMethod]
    public async Task GetIndexAsync_ShouldDescribeTheIndexAsCreated()
    {
        var bucket = this.Storage.Vectors.From(this.VectorBucketName);
        await bucket.CreateIndexAsync(new NewVectorIndex
        {
            IndexName = this.TrackIndex("docs-v2"),
            Dimension = 3,
            DistanceMetric = VectorDistanceMetric.Euclidean,
            NonFilterableMetadataKeys = new[] { "raw_text" },
        });
        var index = await bucket.GetIndexAsync("docs-v2");
        using (new AssertionScope())
        {
            index.Should().BeEquivalentTo(new
            {
                IndexName = "docs-v2",
                Dimension = 3,
                DistanceMetric = VectorDistanceMetric.Euclidean,
                DataType = VectorDataType.Float32,
                NonFilterableMetadataKeys = new[] { "raw_text" },
            }, "the service describes the index with the settings it was created with");
            index.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, ClockTolerance, "the index was created by this test");
        }
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldReturnNoNonFilterableKeys_GivenAnIndexCreatedWithoutAny()
    {
        (await this.Storage.Vectors.From(this.VectorBucketName).GetIndexAsync(IndexName)).NonFilterableMetadataKeys
            .Should().BeEmpty("the service stores no metadata configuration for an index created without one");
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).GetIndexAsync("missing-index"))
            .Should().ThrowAsync<SupabaseStorageException>("the bucket holds no index of that name");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.NotFound);
        }
    }
}
