using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.CreateVectorBucket;

/// <summary>
/// Contract tests for <c>Vectors.CreateVectorBucketAsync</c>: the request it sends to
/// <c>POST /vector/CreateVectorBucket</c> and how each service answer maps to the caller's result.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateVectorBucketContractTests
{
    private const string Path = "/storage/v1/vector/CreateVectorBucket";
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
    public async Task CreateVectorBucketAsync_ShouldPostTheBucketNameToTheVectorRoute()
    {
        this.Respond(200, string.Empty);
        await this.client.Vectors.CreateVectorBucketAsync("embeddings-prod");
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("{\"vectorBucketName\":\"embeddings-prod\"}");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldComplete_GivenAnEmptyBody()
    {
        this.Respond(200, string.Empty);
        await this.client.Vectors.CreateVectorBucketAsync("embeddings-prod");
    }

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldPostToTheVectorRoute_GivenAUrlWithATrailingSlash()
    {
        this.Respond(200, string.Empty);
        await new Client($"{this.server.Url}/storage/v1/").Vectors.CreateVectorBucketAsync("embeddings-prod");
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Path.Should().Be(Path);
    }

    [TestMethod]
    [DataRow(409, "ConflictException", "ConflictException", "vector bucket \"embeddings-prod\" already exists", "the bucket already exists")]
    [DataRow(400, "InvalidRequest", "InvalidRequest", "body must have required property 'vectorBucketName'", "the service rejects an invalid body")]
    [DataRow(400, "S3VectorMaxBucketsExceeded", "S3VectorMaxBucketsExceeded", "Maximum number of buckets exceeded.", "the project reached its bucket quota")]
    [DataRow(500, "Internal", "InternalError", "Internal Server Error", "the service failed internally")]
    public async Task CreateVectorBucketAsync_ShouldThrowStorageException_GivenAServiceError(int status, string error, string code, string message, string because)
    {
        this.Respond(status, JsonSerializer.Serialize(new { statusCode = status.ToString(), error, message, code }));
        var thrown = await FluentActions.Awaiting(() => this.client.Vectors.CreateVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.CreateVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/CreateVectorBucket not found\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.CreateVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, string.Empty);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.client.Vectors.CreateVectorBucketAsync("embeddings-prod", source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task CreateVectorBucketAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, string.Empty);
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.client.Vectors.CreateVectorBucketAsync("embeddings-prod");
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
