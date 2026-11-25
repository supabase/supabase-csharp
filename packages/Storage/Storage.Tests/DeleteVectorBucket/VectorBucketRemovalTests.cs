using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.DeleteVectorBucket;

/// <summary>
/// End-to-end tests for <c>Vectors.DeleteVectorBucketAsync</c> against the vector service of the local stack, on the
/// uniquely named bucket that <see cref="VectorIndexE2EFixture"/> seeds with the index <c>docs-v1</c>.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorBucketRemovalTests : VectorIndexE2EFixture
{
    [TestMethod]
    public async Task DeleteVectorBucketAsync_ShouldRemoveTheBucket_GivenItHoldsNoIndex()
    {
        await this.Storage.Vectors.From(this.VectorBucketName).DeleteIndexAsync(IndexName);
        await this.Storage.Vectors.DeleteVectorBucketAsync(this.VectorBucketName);
        (await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).CreateIndexAsync(
                new NewVectorIndex { IndexName = IndexName, Dimension = 2, DistanceMetric = VectorDistanceMetric.Cosine }))
            .Should().ThrowAsync<SupabaseStorageException>("a deleted bucket can no longer hold an index"))
            .Which.Code.Should().Be("NotFoundException");
    }

    [TestMethod]
    public async Task DeleteVectorBucketAsync_ShouldThrowNotEmpty_GivenTheBucketStillHoldsAnIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.DeleteVectorBucketAsync(this.VectorBucketName))
            .Should().ThrowAsync<SupabaseStorageException>("the service only deletes a bucket without indexes");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("VectorBucketNotEmpty");
        }
    }

    [TestMethod]
    public async Task DeleteVectorBucketAsync_ShouldSucceed_GivenABucketThatDoesNotExist()
    {
        await FluentActions.Awaiting(() => this.Storage.Vectors.DeleteVectorBucketAsync($"e2e-missing-{Guid.NewGuid():N}"))
            .Should().NotThrowAsync("the service only checks that no index remains, so deleting a missing bucket is accepted");
    }
}
