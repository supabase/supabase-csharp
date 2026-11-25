using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.Exceptions;
using Supabase.Storage.ListVectorIndexes;

namespace Storage.Tests.ListVectorIndexes;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).ListIndexesAsync</c> against the vector service of the local stack.
/// The bucket <see cref="VectorIndexE2EFixture"/> seeds gets two more indexes, so it holds <c>docs-v1</c>,
/// <c>docs-v2</c> and <c>notes-v1</c>, all removed whatever the outcome.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorIndexListingTests : VectorIndexE2EFixture
{
    private static readonly string[] IndexNames = { IndexName, "docs-v2", "notes-v1" };

    [TestInitialize]
    public async Task CreateMoreIndexes()
    {
        foreach (var indexName in IndexNames.Skip(1))
        {
            await this.Storage.Vectors.From(this.VectorBucketName).CreateIndexAsync(
                new NewVectorIndex { IndexName = this.TrackIndex(indexName), Dimension = 2, DistanceMetric = VectorDistanceMetric.Cosine });
        }
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldListEveryIndexByName()
    {
        (await this.Storage.Vectors.From(this.VectorBucketName).ListIndexesAsync()).Items.Select(index => index.IndexName)
            .Should().Equal(IndexNames, "the service lists every index of the bucket, sorted by name");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldListOnlyMatchingIndexes_GivenAPrefix()
    {
        (await this.Storage.Vectors.From(this.VectorBucketName).ListIndexesAsync(new ListVectorIndexesOptions { Prefix = "docs-" })).Items
            .Select(index => index.IndexName).Should().Equal(new[] { "docs-v1", "docs-v2" }, "only names starting with the prefix match");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldPageThroughEveryIndex_GivenMaxResults()
    {
        var bucket = this.Storage.Vectors.From(this.VectorBucketName);
        var first = await bucket.ListIndexesAsync(new ListVectorIndexesOptions { MaxResults = 2 });
        var last = await bucket.ListIndexesAsync(new ListVectorIndexesOptions { MaxResults = 2, NextToken = first.NextToken });
        using (new AssertionScope())
        {
            first.Items.Select(index => index.IndexName).Should().Equal(new[] { "docs-v1", "docs-v2" });
            last.Items.Select(index => index.IndexName).Should().Equal(new[] { "notes-v1" }, "the token resumes after the previous page");
            last.NextToken.Should().BeNull("no index remains after the last page");
        }
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldReturnNoIndex_GivenABucketThatDoesNotExist()
    {
        (await this.Storage.Vectors.From($"e2e-missing-{Guid.NewGuid():N}").ListIndexesAsync()).Items
            .Should().BeEmpty("the service does not check that the bucket exists before listing");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldThrowInvalidInput_GivenAnOutOfRangeMaxResults()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.From(this.VectorBucketName).ListIndexesAsync(new ListVectorIndexesOptions { MaxResults = 0 }))
            .Should().ThrowAsync<SupabaseStorageException>("the service accepts 1 to 500 results per page");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.InvalidInput);
        }
    }
}
