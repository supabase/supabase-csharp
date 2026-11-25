using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage.Exceptions;
using Supabase.Storage.ListVectorBuckets;

namespace Storage.Tests.ListVectorBuckets;

/// <summary>
/// End-to-end tests for <c>Vectors.ListVectorBucketsAsync</c> against the vector service of the local stack. Each test
/// seeds three buckets that share a unique prefix over plain HTTP, and removes them whatever the outcome.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorBucketListingTests : VectorIndexE2EFixture
{
    private string prefix = null!;
    private string[] names = null!;

    [TestInitialize]
    public async Task CreateBuckets()
    {
        this.prefix = $"list-{this.VectorBucketName}";
        this.names = new[] { "a", "b", "c" }.Select(suffix => $"{this.prefix}-{suffix}").ToArray();
        foreach (var name in this.names)
            await this.Storage.Vectors.CreateVectorBucketAsync(name);
    }

    [TestCleanup]
    public async Task RemoveBuckets()
    {
        foreach (var name in this.names)
            await this.Storage.Vectors.DeleteVectorBucketAsync(name);
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldListOnlyMatchingBucketsByName_GivenAPrefix()
    {
        var page = await this.Storage.Vectors.ListVectorBucketsAsync(new ListVectorBucketsOptions { Prefix = this.prefix });
        using (new AssertionScope())
        {
            page.Items.Select(bucket => bucket.VectorBucketName).Should().Equal(this.names, "only names starting with the prefix match, sorted by name");
            page.NextToken.Should().BeNull("every match fits on the default page");
            page.Items.Should().OnlyContain(bucket => bucket.CreatedAt.Year >= 2025, "the service reports the creation time");
        }
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldPageThroughEveryBucket_GivenMaxResults()
    {
        var options = new ListVectorBucketsOptions { Prefix = this.prefix, MaxResults = 2 };
        var first = await this.Storage.Vectors.ListVectorBucketsAsync(options);
        var last = await this.Storage.Vectors.ListVectorBucketsAsync(options with { NextToken = first.NextToken });
        using (new AssertionScope())
        {
            first.Items.Select(bucket => bucket.VectorBucketName).Should().Equal(this.names.Take(2));
            last.Items.Select(bucket => bucket.VectorBucketName).Should().Equal(new[] { this.names[2] }, "the token resumes after the previous page");
            last.NextToken.Should().BeNull("no bucket remains after the last page");
        }
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldReturnNoBucket_GivenAPrefixNothingMatches()
    {
        (await this.Storage.Vectors.ListVectorBucketsAsync(new ListVectorBucketsOptions { Prefix = $"{this.prefix}-zzz" })).Items
            .Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldThrowInvalidInput_GivenAnOutOfRangeMaxResults()
    {
        var thrown = await FluentActions.Awaiting(() => this.Storage.Vectors.ListVectorBucketsAsync(new ListVectorBucketsOptions { MaxResults = 0 }))
            .Should().ThrowAsync<SupabaseStorageException>("the service accepts 1 to 500 results per page");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.InvalidInput);
        }
    }
}
