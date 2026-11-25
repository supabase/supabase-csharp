using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Exceptions;

namespace Storage.Tests.DeleteVectorIndex;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).DeleteIndexAsync</c> against the vector service of the local stack,
/// in the uniquely named bucket that <see cref="VectorIndexE2EFixture"/> seeds with the index <c>docs-v1</c>.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorIndexRemovalTests : VectorIndexE2EFixture
{
    [TestMethod]
    public async Task DeleteIndexAsync_ShouldRemoveTheIndexFromTheBucket()
    {
        var bucket = this.Storage.Vectors.From(this.VectorBucketName);
        await bucket.DeleteIndexAsync(IndexName);
        (await bucket.ListIndexesAsync()).Items.Select(index => index.IndexName)
            .Should().NotContain(IndexName, "a deleted index is no longer part of its bucket");
    }

    [TestMethod]
    public async Task DeleteIndexAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).DeleteIndexAsync("missing-index"))
            .Should().ThrowAsync<SupabaseStorageException>("the bucket holds no index of that name");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.NotFound);
        }
    }
}
