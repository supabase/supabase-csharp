using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Exceptions;
using Supabase.Storage.VectorBuckets.Common;
using Supabase.Storage.VectorBuckets.CreateIndex;

namespace Storage.Tests.VectorBuckets.CreateBucket;

/// <summary>
/// End-to-end tests for <c>Vectors.CreateVectorBucketAsync</c> against the vector service of the local stack, on the
/// uniquely named bucket that <see cref="VectorIndexE2EFixture"/> removes after each test. The fixture's own seeded
/// bucket is left aside: each test creates the bucket under test itself.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorBucketCreationTests : VectorIndexE2EFixture
{
    private string NewBucketName() => $"{this.VectorBucketName}-new";

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldCreateABucketThatHoldsIndexes()
    {
        var name = this.NewBucketName();
        await this.Storage.Vectors.CreateVectorBucketAsync(name);
        try
        {
            await FluentActions.Awaiting(() => this.Storage.Vectors.From(name).CreateIndexAsync(
                    new NewVectorIndex { IndexName = IndexName, Dimension = 2, DistanceMetric = VectorDistanceMetric.Cosine }))
                .Should().NotThrowAsync("the bucket exists once the call returns");
        }
        finally
        {
            await this.Storage.Vectors.From(name).DeleteIndexAsync(IndexName);
            await this.Storage.Vectors.DeleteVectorBucketAsync(name);
        }
    }

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldThrowConflict_GivenTheBucketAlreadyExists()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.CreateVectorBucketAsync(this.VectorBucketName))
            .Should().ThrowAsync<SupabaseStorageException>("the fixture already created this bucket");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(409);
            thrown.Which.Code.Should().Be("ConflictException");
        }
    }
}
