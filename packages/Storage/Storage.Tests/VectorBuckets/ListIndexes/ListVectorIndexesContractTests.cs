using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using Supabase.Storage.VectorBuckets.Common;
using Supabase.Storage.VectorBuckets.ListIndexes;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.VectorBuckets.ListIndexes;

/// <summary>
/// Contract tests for <c>Vectors.From(bucket).ListIndexesAsync</c>: the request it sends to
/// <c>POST /vector/ListIndexes</c> and how each service answer maps to the caller's page.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListVectorIndexesContractTests
{
    private const string Path = "/storage/v1/vector/ListIndexes";
    private const long FirstCreationTime = 1795590525;
    private const long SecondCreationTime = 1795590600;
    private const string FirstPageBody = "{\"indexes\":[{\"indexName\":\"docs-v1\",\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":1795590525},{\"indexName\":\"docs-v2\",\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":1795590600}],\"nextToken\":\"docs-v2\"}";

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
    public async Task ListIndexesAsync_ShouldPostOnlyTheBucketName_GivenNoOptions()
    {
        this.Respond(200, FirstPageBody);
        await this.client.Vectors.From("embeddings-prod").ListIndexesAsync();
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("{\"vectorBucketName\":\"embeddings-prod\"}", "an unset option is left to the service's default");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldSendThePrefixAndPaging_GivenOptions()
    {
        this.Respond(200, FirstPageBody);
        await this.client.Vectors.From("embeddings-prod").ListIndexesAsync(new ListVectorIndexesOptions { Prefix = "docs-", MaxResults = 2, NextToken = "docs-a" });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body
            .Should().Be("{\"vectorBucketName\":\"embeddings-prod\",\"maxResults\":2,\"nextToken\":\"docs-a\",\"prefix\":\"docs-\"}", "the ListIndexes schema names them maxResults, nextToken and prefix");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldReturnThePage()
    {
        this.Respond(200, FirstPageBody);
        (await this.client.Vectors.From("embeddings-prod").ListIndexesAsync()).Should().BeEquivalentTo(new VectorListPage<VectorIndexSummary>
        {
            Items = new[]
            {
                new VectorIndexSummary { IndexName = "docs-v1", CreatedAt = DateTimeOffset.FromUnixTimeSeconds(FirstCreationTime) },
                new VectorIndexSummary { IndexName = "docs-v2", CreatedAt = DateTimeOffset.FromUnixTimeSeconds(SecondCreationTime) },
            },
            NextToken = "docs-v2",
        }, options => options.WithStrictOrdering(), "the service lists indexes by name with creation times in Unix seconds and a token while more remain");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldReturnNoNextToken_GivenTheLastPage()
    {
        this.Respond(200, $"{{\"indexes\":[{{\"indexName\":\"docs-v3\",\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":{FirstCreationTime}}}]}}");
        (await this.client.Vectors.From("embeddings-prod").ListIndexesAsync()).NextToken
            .Should().BeNull("the service omits the token when no index remains");
    }

    [TestMethod]
    [DataRow("{\"indexes\":[]}", "the bucket has no index, or does not exist")]
    [DataRow("{}", "the service left the list out")]
    public async Task ListIndexesAsync_ShouldReturnNoItems_GivenNoIndexListed(string body, string because)
    {
        this.Respond(200, body);
        (await this.client.Vectors.From("embeddings-prod").ListIndexesAsync()).Items.Should().BeEmpty(because);
    }

    [TestMethod]
    [DataRow("null", "the service answered with no page")]
    [DataRow("{\"indexes\":[{\"indexName\":\"docs-v1\"}]}", "a listed index lacks its creation time")]
    [DataRow("{\"indexes\":[{\"creationTime\":1795590525}]}", "a listed index lacks its name")]
    [DataRow("<html>", "the answer is not JSON")]
    public async Task ListIndexesAsync_ShouldThrowStorageException_GivenAnUnreadableAnswer(string body, string because)
    {
        this.Respond(200, body);
        await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").ListIndexesAsync())
            .Should().ThrowAsync<SupabaseStorageException>(because);
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects a maxResults outside 1 to 500")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task ListIndexesAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").ListIndexesAsync())
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldClassifyAsInvalidInput_GivenAnOutOfRangeMaxResults()
    {
        this.Respond(400, "{\"statusCode\":\"400\",\"error\":\"Error\",\"message\":\"body/maxResults must be >= 1\",\"code\":\"InvalidRequest\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").ListIndexesAsync(new ListVectorIndexesOptions { MaxResults = 0 }))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.InvalidInput);
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").ListIndexesAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/ListIndexes not found\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").ListIndexesAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, FirstPageBody);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").ListIndexesAsync(cancellationToken: source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task ListIndexesAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, FirstPageBody);
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.client.Vectors.From("embeddings-prod").ListIndexesAsync();
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    [TestMethod]
    public void ListIndexesAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, FirstPageBody);
        BlockingCaller.Completes(() => this.client.Vectors.From("embeddings-prod").ListIndexesAsync())
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
