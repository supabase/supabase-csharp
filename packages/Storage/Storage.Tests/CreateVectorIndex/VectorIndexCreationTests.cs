using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.CreateVectorIndex;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).CreateIndexAsync</c> against the vector service of the local stack,
/// in the uniquely named bucket that <see cref="VectorIndexE2EFixture"/> seeds with the index <c>docs-v1</c>.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorIndexCreationTests : VectorIndexE2EFixture
{
    private static readonly NewVectorIndex SecondIndex = new()
    {
        IndexName = "docs-v2",
        Dimension = 3,
        DistanceMetric = VectorDistanceMetric.Euclidean,
        NonFilterableMetadataKeys = new[] { "raw_text" },
    };

    [TestMethod]
    public async Task CreateIndexAsync_ShouldStoreTheIndexWithItsSettings()
    {
        var bucket = this.Storage.Vectors.From(this.VectorBucketName);
        this.TrackIndex(SecondIndex.IndexName);
        await bucket.CreateIndexAsync(SecondIndex);
        (await bucket.GetIndexAsync(SecondIndex.IndexName)).Should().BeEquivalentTo(new
        {
            SecondIndex.IndexName,
            SecondIndex.Dimension,
            SecondIndex.DistanceMetric,
            SecondIndex.DataType,
            SecondIndex.NonFilterableMetadataKeys,
        }, "the service keeps every setting the index was created with");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldThrowConflict_GivenAnIndexOfThatNameAlreadyExists()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).CreateIndexAsync(
                new NewVectorIndex { IndexName = IndexName, Dimension = 2, DistanceMetric = VectorDistanceMetric.Cosine }))
            .Should().ThrowAsync<SupabaseStorageException>("index names are unique within a vector bucket");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(409);
            thrown.Which.Code.Should().Be("ConflictException");
        }
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldThrowNotFound_GivenABucketThatDoesNotExist()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From($"e2e-missing-{Guid.NewGuid():N}").CreateIndexAsync(SecondIndex))
            .Should().ThrowAsync<SupabaseStorageException>("an index can only be created in an existing bucket");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.NotFound);
        }
    }
}
