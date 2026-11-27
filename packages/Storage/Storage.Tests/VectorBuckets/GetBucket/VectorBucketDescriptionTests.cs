using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.VectorBuckets.GetBucket;

/// <summary>
/// End-to-end tests for <c>Vectors.GetVectorBucketAsync</c> against the vector service of the local stack, in the
/// uniquely named bucket that <see cref="VectorIndexE2EFixture"/> seeds and removes.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorBucketDescriptionTests : VectorIndexE2EFixture
{
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldDescribeTheBucketAsCreated()
    {
        var bucket = await this.Storage.Vectors.GetVectorBucketAsync(this.VectorBucketName);
        using (new AssertionScope())
        {
            bucket.VectorBucketName.Should().Be(this.VectorBucketName);
            bucket.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, ClockTolerance, "the bucket was created by this test");
        }
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldThrowNotFound_GivenAMissingBucket()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.GetVectorBucketAsync($"missing-{this.VectorBucketName}"))
            .Should().ThrowAsync<SupabaseStorageException>("the project holds no bucket of that name");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.NotFound);
        }
    }
}
