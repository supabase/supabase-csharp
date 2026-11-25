using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.Exceptions;
using Supabase.Storage.ListVectorBuckets;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.ListVectorBuckets;

/// <summary>
/// Contract tests for <c>Vectors.ListVectorBucketsAsync</c>: the request it sends to
/// <c>POST /vector/ListVectorBuckets</c> and how each service answer maps to the caller's page.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListVectorBucketsContractTests
{
    private const string Path = "/storage/v1/vector/ListVectorBuckets";
    private const long FirstCreationTime = 1795590525;
    private const long SecondCreationTime = 1795590600;
    private const string FirstPageBody = "{\"vectorBuckets\":[{\"vectorBucketName\":\"embeddings-v1\",\"creationTime\":1795590525},{\"vectorBucketName\":\"embeddings-v2\",\"creationTime\":1795590600}],\"nextToken\":\"embeddings-v2\"}";

    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/storage/v1", new Dictionary<string, string> { { "Authorization", "Bearer service-role-key" } });
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldPostAnEmptyBody_GivenNoOptions()
    {
        this.Respond(200, FirstPageBody);
        await this.client.Vectors.ListVectorBucketsAsync();
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("{}", "an unset option is left to the service's default");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldSendThePrefixAndPaging_GivenOptions()
    {
        this.Respond(200, FirstPageBody);
        await this.client.Vectors.ListVectorBucketsAsync(new ListVectorBucketsOptions { Prefix = "embeddings-", MaxResults = 2, NextToken = "embeddings-a" });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body
            .Should().Be("{\"maxResults\":2,\"nextToken\":\"embeddings-a\",\"prefix\":\"embeddings-\"}", "the ListVectorBuckets schema names them maxResults, nextToken and prefix");
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldReturnThePage()
    {
        this.Respond(200, FirstPageBody);
        (await this.client.Vectors.ListVectorBucketsAsync()).Should().BeEquivalentTo(new VectorListPage<VectorBucketSummary>
        {
            Items = new[]
            {
                new VectorBucketSummary { VectorBucketName = "embeddings-v1", CreatedAt = DateTimeOffset.FromUnixTimeSeconds(FirstCreationTime) },
                new VectorBucketSummary { VectorBucketName = "embeddings-v2", CreatedAt = DateTimeOffset.FromUnixTimeSeconds(SecondCreationTime) },
            },
            NextToken = "embeddings-v2",
        }, options => options.WithStrictOrdering(), "the service lists buckets by name with creation times in Unix seconds and a token while more remain");
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldReturnNoNextToken_GivenTheLastPage()
    {
        this.Respond(200, $"{{\"vectorBuckets\":[{{\"vectorBucketName\":\"embeddings-v3\",\"creationTime\":{FirstCreationTime}}}]}}");
        (await this.client.Vectors.ListVectorBucketsAsync()).NextToken
            .Should().BeNull("the service omits the token when no bucket remains");
    }

    [TestMethod]
    [DataRow("{\"vectorBuckets\":[]}", "the project has no vector bucket")]
    [DataRow("{}", "the service left the list out")]
    public async Task ListVectorBucketsAsync_ShouldReturnNoItems_GivenNoBucketListed(string body, string because)
    {
        this.Respond(200, body);
        (await this.client.Vectors.ListVectorBucketsAsync()).Items.Should().BeEmpty(because);
    }

    [TestMethod]
    [DataRow("null", "the service answered with no page")]
    [DataRow("{\"vectorBuckets\":[{\"vectorBucketName\":\"embeddings-v1\"}]}", "a listed bucket lacks its creation time")]
    [DataRow("{\"vectorBuckets\":[{\"creationTime\":1795590525}]}", "a listed bucket lacks its name")]
    [DataRow("<html>", "the answer is not JSON")]
    public async Task ListVectorBucketsAsync_ShouldThrowStorageException_GivenAnUnreadableAnswer(string body, string because)
    {
        this.Respond(200, body);
        await FluentActions.Awaiting(() => this.client.Vectors.ListVectorBucketsAsync())
            .Should().ThrowAsync<SupabaseStorageException>(because);
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects a maxResults outside 1 to 500")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task ListVectorBucketsAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.client.Vectors.ListVectorBucketsAsync())
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldClassifyAsInvalidInput_GivenAnOutOfRangeMaxResults()
    {
        this.Respond(400, "{\"statusCode\":\"400\",\"error\":\"Error\",\"message\":\"body/maxResults must be >= 1\",\"code\":\"InvalidRequest\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.ListVectorBucketsAsync(new ListVectorBucketsOptions { MaxResults = 0 }))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.InvalidInput);
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.ListVectorBucketsAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/ListVectorBuckets not found\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.ListVectorBucketsAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, FirstPageBody);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.client.Vectors.ListVectorBucketsAsync(cancellationToken: source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task ListVectorBucketsAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, FirstPageBody);
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.client.Vectors.ListVectorBucketsAsync();
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    [TestMethod]
    public void ListVectorBucketsAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, FirstPageBody);
        BlockingCaller.Completes(() => this.client.Vectors.ListVectorBucketsAsync())
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
