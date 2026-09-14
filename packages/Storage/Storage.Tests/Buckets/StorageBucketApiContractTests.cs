using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.Buckets;

/// <summary>
/// Contract tests for the bucket control-plane HTTP calls the client builds (path, method and JSON
/// body per operation) and how it reads the responses — including that <c>GetBucket</c> swallows a
/// not-found into a null result but lets any other failure surface as a
/// <see cref="SupabaseStorageException"/>.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageBucketApiContractTests
{
    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/storage/v1", new Dictionary<string, string>
        {
            { "Authorization", "Bearer test-key" }
        });
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task ListBuckets_ShouldGetTheBucketCollection()
    {
        this.Respond("/storage/v1/bucket", "GET", 200, "[{\"id\":\"a\",\"name\":\"a\"}]");
        var buckets = await this.client.ListBuckets();
        using (new AssertionScope())
        {
            buckets.Should().ContainSingle().Which.Name.Should().Be("a");
            var request = this.SingleRequest();
            request.Method.Should().Be("GET");
            request.Path.Should().Be("/storage/v1/bucket");
        }
    }

    [TestMethod]
    public async Task GetBucket_ShouldReturnTheBucket_GivenFound()
    {
        this.Respond("/storage/v1/bucket/photos", "GET", 200, "{\"id\":\"photos\",\"name\":\"photos\",\"public\":true}");
        var bucket = await this.client.GetBucket("photos");
        using (new AssertionScope())
        {
            bucket.Should().NotBeNull();
            bucket!.Id.Should().Be("photos");
            bucket.Public.Should().BeTrue();
        }
    }

    [TestMethod]
    public async Task GetBucket_ShouldReturnNull_GivenNotFound()
    {
        this.Respond("/storage/v1/bucket/missing", "GET", 404, "{\"statusCode\":404,\"message\":\"Bucket not found\"}");
        (await this.client.GetBucket("missing")).Should().BeNull();
    }

    [TestMethod]
    public async Task GetBucket_ShouldThrow_GivenNonNotFoundError()
    {
        this.Respond("/storage/v1/bucket/boom", "GET", 500, "{\"statusCode\":500,\"message\":\"internal\"}");
        var act = () => this.client.GetBucket("boom");
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task CreateBucket_ShouldPostTheBucketAndReturnItsName()
    {
        this.Respond("/storage/v1/bucket", "POST", 200, "{\"name\":\"photos\"}");
        var name = await this.client.CreateBucket("photos", new BucketUpsertOptions { Public = true });
        using (new AssertionScope())
        {
            name.Should().Be("photos");
            var request = this.SingleRequest();
            request.Method.Should().Be("POST");
            request.Path.Should().Be("/storage/v1/bucket");
            request.Body.Should().Contain("\"id\":\"photos\"").And.Contain("\"public\":true");
        }
    }

    [TestMethod]
    public async Task UpdateBucket_ShouldPutToTheBucketId()
    {
        this.Respond("/storage/v1/bucket/photos", "PUT", 200, "{\"id\":\"photos\",\"public\":true}");
        await this.client.UpdateBucket("photos", new BucketUpsertOptions { Public = true });
        using (new AssertionScope())
        {
            var request = this.SingleRequest();
            request.Method.Should().Be("PUT");
            request.Path.Should().Be("/storage/v1/bucket/photos");
            request.Body.Should().Contain("\"public\":true");
        }
    }

    [TestMethod]
    public async Task EmptyBucket_ShouldPostToTheEmptyPath()
    {
        this.Respond("/storage/v1/bucket/photos/empty", "POST", 200, "{\"message\":\"Successfully emptied\"}");
        await this.client.EmptyBucket("photos");
        using (new AssertionScope())
        {
            this.SingleRequest().Method.Should().Be("POST");
            this.SingleRequest().Path.Should().Be("/storage/v1/bucket/photos/empty");
        }
    }

    [TestMethod]
    public async Task DeleteBucket_ShouldDeleteTheBucketId()
    {
        this.Respond("/storage/v1/bucket/photos", "DELETE", 200, "{\"message\":\"Successfully deleted\"}");
        await this.client.DeleteBucket("photos");
        using (new AssertionScope())
        {
            this.SingleRequest().Method.Should().Be("DELETE");
            this.SingleRequest().Path.Should().Be("/storage/v1/bucket/photos");
        }
    }

    [TestMethod]
    public async Task PurgeBucketCache_ShouldDeleteTheCdnPathWithNoBodyAndReturnTheMessage()
    {
        this.Respond("/storage/v1/cdn/photos", "DELETE", 200, "{\"message\":\"success\"}");
        var response = await this.client.PurgeBucketCache("photos", new PurgeCacheOptions { Transformations = true });
        using (new AssertionScope())
        {
            response!.Message.Should().Be("success");
            var request = this.SingleRequest();
            request.Method.Should().Be("DELETE");
            request.Path.Should().Be("/storage/v1/cdn/photos");
            request.Body.Should().BeNullOrEmpty("options travel in the query string, so the purge carries no payload");
        }
    }

    [TestMethod]
    public async Task PurgeBucketCache_ShouldPurgeEveryVersion_GivenDefaultOptions()
    {
        this.Respond("/storage/v1/cdn/photos", "DELETE", 200, "{\"message\":\"success\"}");
        await this.client.PurgeBucketCache("photos", new PurgeCacheOptions());
        this.SingleRequest().Query.Should().BeNullOrEmpty(
            "unset options must purge every cached version, not only the transformations");
    }

    [TestMethod]
    public async Task PurgeBucketCache_ShouldRequestTransformationsOnly_GivenTheTransformationsOption()
    {
        this.Respond("/storage/v1/cdn/photos", "DELETE", 200, "{\"message\":\"success\"}");
        await this.client.PurgeBucketCache("photos", new PurgeCacheOptions { Transformations = true });
        this.SingleRequest().Query.Should().Contain(pair => pair.Key == "transformations" && pair.Value.Contains("true"));
    }

    [TestMethod]
    public async Task PurgeBucketCache_ShouldEncodeDelimiters_GivenABucketIdWithUrlDelimiters()
    {
        this.Respond("/storage/v1/cdn/*", "DELETE", 200, "{\"message\":\"success\"}");
        await this.client.PurgeBucketCache("my?bucket");
        this.SingleRequest().AbsoluteUrl.Should().EndWith("/storage/v1/cdn/my%3Fbucket",
            "a raw '?' in the id would start a query string and purge the wrong bucket");
    }

    private void Respond(string path, string method, int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));

    private IRequestMessage SingleRequest() => this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
}
