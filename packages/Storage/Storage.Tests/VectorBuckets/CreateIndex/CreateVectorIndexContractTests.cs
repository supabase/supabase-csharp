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
using Supabase.Storage.VectorBuckets.Common;
using Supabase.Storage.VectorBuckets.CreateIndex;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.VectorBuckets.CreateIndex;

/// <summary>
/// Contract tests for <c>Vectors.From(bucket).CreateIndexAsync</c>: the request it sends to
/// <c>POST /vector/CreateIndex</c> and how each service answer maps to the caller's result.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateVectorIndexContractTests
{
    private const string Path = "/storage/v1/vector/CreateIndex";
    private static readonly NewVectorIndex Index = new()
    {
        IndexName = "docs-v1",
        Dimension = 1536,
        DistanceMetric = VectorDistanceMetric.Cosine,
        NonFilterableMetadataKeys = new[] { "raw_text" },
    };

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
    public async Task CreateIndexAsync_ShouldPostTheIndexSettingsToTheVectorRoute()
    {
        this.Respond(200, string.Empty);
        await this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("{\"vectorBucketName\":\"embeddings-prod\",\"indexName\":\"docs-v1\",\"dimension\":1536,\"distanceMetric\":\"cosine\",\"dataType\":\"float32\",\"metadataConfiguration\":{\"nonFilterableMetadataKeys\":[\"raw_text\"]}}");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldPassAnEmptyKeyListThrough_GivenNoNonFilterableKeys()
    {
        this.Respond(200, string.Empty);
        await this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index with { NonFilterableMetadataKeys = Array.Empty<string>() });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body
            .Should().Contain("\"metadataConfiguration\":{\"nonFilterableMetadataKeys\":[]}", "the service, not the SDK, judges an empty list");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldSendTheEuclideanMetricByItsServiceName()
    {
        this.Respond(200, string.Empty);
        await this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index with { DistanceMetric = VectorDistanceMetric.Euclidean });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body
            .Should().Contain("\"distanceMetric\":\"euclidean\"", "the service's CreateIndex schema names the metric euclidean");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldLeaveMissingSettingsToTheService_GivenNullMetricAndDataType()
    {
        this.Respond(200, string.Empty);
        await this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index with { DistanceMetric = null!, DataType = null! });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body
            .Should().Contain("\"distanceMetric\":null,\"dataType\":null", "the service, not the SDK, validates the index settings");
    }

    [TestMethod]
    public void From_ShouldNotSendARequest()
    {
        this.client.Vectors.From("embeddings-prod");
        this.server.LogEntries.Should().BeEmpty("scoping to a bucket is local; only an operation reaches the service");
    }

    [TestMethod]
    [DataRow(400, "Error", "InvalidRequest", "body/dimension must be >= 1", "the service rejects invalid settings")]
    [DataRow(404, "NotFoundException", "NotFoundException", "resource \"embeddings-prod\" not found", "the bucket does not exist")]
    [DataRow(409, "ConflictException", "ConflictException", "vector index \"docs-v1\" already exists", "the bucket already holds an index of that name")]
    [DataRow(500, "Internal", "InternalError", "Internal Server Error", "the service failed internally")]
    public async Task CreateIndexAsync_ShouldThrowStorageException_GivenAServiceError(int status, string error, string code, string message, string because)
    {
        this.Respond(status, JsonSerializer.Serialize(new { statusCode = status.ToString(), error, message, code }));
        var thrown = await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index))
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/CreateIndex not found\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, string.Empty);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index, source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task CreateIndexAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, string.Empty);
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.client.Vectors.From("embeddings-prod").CreateIndexAsync(Index);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
