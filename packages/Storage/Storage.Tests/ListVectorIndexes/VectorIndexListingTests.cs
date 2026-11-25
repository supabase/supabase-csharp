using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.CreateVectorIndex;
using Supabase.Storage.Exceptions;
using Supabase.Storage.ListVectorIndexes;

namespace Storage.Tests.ListVectorIndexes;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).ListIndexesAsync</c> against the vector service of the local stack.
/// Each test seeds its own uniquely named vector bucket through the service's <c>CreateVectorBucket</c> route, and
/// the bucket and its indexes are removed whatever the outcome.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorIndexListingTests
{
    private static readonly string[] IndexNames = { "docs-v1", "docs-v2", "notes-v1" };
    private readonly Client storage = Helpers.GetServiceClient();
    private readonly string vectorBucketName = $"e2e-{Guid.NewGuid():N}";

    [TestInitialize]
    public async Task SeedVectorBucket()
    {
        await CreateVectorBucket(this.vectorBucketName);
        foreach (var indexName in IndexNames)
        {
            await this.storage.Vectors.From(this.vectorBucketName).CreateIndexAsync(
                new NewVectorIndex { IndexName = indexName, Dimension = 3, DistanceMetric = VectorDistanceMetric.Cosine });
        }
    }

    [TestCleanup]
    public async Task RemoveVectorBucket()
    {
        foreach (var indexName in IndexNames)
        {
            await IgnoreServiceRefusal(() => this.storage.Vectors.From(this.vectorBucketName).DeleteIndexAsync(indexName));
        }
        await IgnoreServiceRefusal(() => this.storage.Vectors.DeleteVectorBucketAsync(this.vectorBucketName));
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldListEveryIndexByName()
    {
        (await this.storage.Vectors.From(this.vectorBucketName).ListIndexesAsync()).Items.Select(index => index.IndexName)
            .Should().Equal(IndexNames, "the service lists every index of the bucket, sorted by name");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldListOnlyMatchingIndexes_GivenAPrefix()
    {
        (await this.storage.Vectors.From(this.vectorBucketName).ListIndexesAsync(new ListVectorIndexesOptions { Prefix = "docs-" })).Items
            .Select(index => index.IndexName).Should().Equal(new[] { "docs-v1", "docs-v2" }, "only names starting with the prefix match");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldPageThroughEveryIndex_GivenMaxResults()
    {
        var bucket = this.storage.Vectors.From(this.vectorBucketName);
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
        (await this.storage.Vectors.From($"e2e-missing-{Guid.NewGuid():N}").ListIndexesAsync()).Items
            .Should().BeEmpty("the service does not check that the bucket exists before listing");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldThrowInvalidInput_GivenAnOutOfRangeMaxResults()
    {
        var thrown = await FluentActions.Awaiting(() => this.storage.Vectors.From(this.vectorBucketName).ListIndexesAsync(new ListVectorIndexesOptions { MaxResults = 0 }))
            .Should().ThrowAsync<SupabaseStorageException>("the service accepts 1 to 500 results per page");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Code.Should().Be("InvalidRequest");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.InvalidInput);
        }
    }

    private static async Task CreateVectorBucket(string name)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Helpers.StorageUrl}/vector/CreateVectorBucket")
        {
            Content = new StringContent($"{{\"vectorBucketName\":\"{name}\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Helpers.ServiceKey);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task IgnoreServiceRefusal(Func<Task> removal)
    {
        try
        {
            await removal();
        }
        catch (SupabaseStorageException)
        {
        }
    }
}
