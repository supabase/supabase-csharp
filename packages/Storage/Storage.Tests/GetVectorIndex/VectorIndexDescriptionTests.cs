using System;
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

namespace Storage.Tests.GetVectorIndex;

/// <summary>
/// End-to-end tests for <c>Vectors.From(bucket).GetIndexAsync</c> against the vector service of the local stack.
/// Each test seeds its own uniquely named vector bucket through the service's <c>CreateVectorBucket</c> route, and
/// the bucket and its index are removed whatever the outcome.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VectorIndexDescriptionTests
{
    private const string IndexName = "docs-v1";
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);
    private readonly Client storage = Helpers.GetServiceClient();
    private readonly string vectorBucketName = $"e2e-{Guid.NewGuid():N}";

    [TestInitialize]
    public Task SeedVectorBucket() => CreateVectorBucket(this.vectorBucketName);

    [TestCleanup]
    public async Task RemoveVectorBucket()
    {
        await IgnoreServiceRefusal(() => this.storage.Vectors.From(this.vectorBucketName).DeleteIndexAsync(IndexName));
        await IgnoreServiceRefusal(() => this.storage.Vectors.DeleteVectorBucketAsync(this.vectorBucketName));
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldDescribeTheIndexAsCreated()
    {
        var bucket = this.storage.Vectors.From(this.vectorBucketName);
        await bucket.CreateIndexAsync(new NewVectorIndex
        {
            IndexName = IndexName,
            Dimension = 3,
            DistanceMetric = VectorDistanceMetric.Euclidean,
            NonFilterableMetadataKeys = new[] { "raw_text" },
        });
        var index = await bucket.GetIndexAsync(IndexName);
        using (new AssertionScope())
        {
            index.Should().BeEquivalentTo(new
            {
                IndexName,
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
        var bucket = this.storage.Vectors.From(this.vectorBucketName);
        await bucket.CreateIndexAsync(new NewVectorIndex { IndexName = IndexName, Dimension = 3, DistanceMetric = VectorDistanceMetric.Cosine });
        (await bucket.GetIndexAsync(IndexName)).NonFilterableMetadataKeys
            .Should().BeEmpty("the service stores no metadata configuration for an index created without one");
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldThrowNotFound_GivenAMissingIndex()
    {
        var thrown = await FluentActions.Awaiting(() => this.storage.Vectors.From(this.vectorBucketName).GetIndexAsync(IndexName))
            .Should().ThrowAsync<SupabaseStorageException>("the bucket holds no index of that name");
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(404);
            thrown.Which.Code.Should().Be("NotFoundException");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.NotFound);
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
