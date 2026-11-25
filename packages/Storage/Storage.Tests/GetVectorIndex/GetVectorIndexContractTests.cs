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
using Supabase.Storage.GetVectorIndex;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.GetVectorIndex;

/// <summary>
/// Contract tests for <c>Vectors.From(bucket).GetIndexAsync</c>: the request it sends to
/// <c>POST /vector/GetIndex</c> and how each service answer maps to the caller's result.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetVectorIndexContractTests
{
    private const string Path = "/storage/v1/vector/GetIndex";
    private const long CreationTime = 1795590525;

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
    public async Task GetIndexAsync_ShouldPostTheBucketAndIndexNamesToTheVectorRoute()
    {
        this.Respond(200, IndexBody("\"cosine\"", "\"float32\"", "null"));
        await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1");
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("{\"vectorBucketName\":\"embeddings-prod\",\"indexName\":\"docs-v1\"}");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldReturnTheIndexDetails()
    {
        this.Respond(200, IndexBody("\"cosine\"", "\"float32\"", "{\"nonFilterableMetadataKeys\":[\"raw_text\",\"source\"]}"));
        (await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1")).Should().BeEquivalentTo(new VectorIndexDetails
        {
            IndexName = "docs-v1",
            Dimension = 1536,
            DistanceMetric = VectorDistanceMetric.Cosine,
            DataType = VectorDataType.Float32,
            NonFilterableMetadataKeys = new[] { "raw_text", "source" },
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(CreationTime),
        }, "the service describes the index with its settings and a creation time in Unix seconds");
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldDecodeTheEuclideanMetric()
    {
        this.Respond(200, IndexBody("\"euclidean\"", "\"float32\"", "null"));
        (await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1")).DistanceMetric
            .Should().Be(VectorDistanceMetric.Euclidean, "the service names the metric euclidean");
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldKeepTheServiceName_GivenAnUnknownDistanceMetric()
    {
        this.Respond(200, IndexBody("\"dotproduct\"", "\"float32\"", "null"));
        var metric = (await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1")).DistanceMetric;
        using (new AssertionScope())
        {
            metric.ToString().Should().Be("dotproduct", "decoding is open to new values and keeps the raw value (SDK principles)");
            metric.Should().NotBe(VectorDistanceMetric.Cosine).And.NotBe(VectorDistanceMetric.Euclidean);
        }
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldKeepTheServiceName_GivenAnUnknownDataType()
    {
        this.Respond(200, IndexBody("\"cosine\"", "\"float16\"", "null"));
        var dataType = (await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1")).DataType;
        using (new AssertionScope())
        {
            dataType.ToString().Should().Be("float16", "decoding is open to new values and keeps the raw value (SDK principles)");
            dataType.Should().NotBe(VectorDataType.Float32);
        }
    }

    [TestMethod]
    [DataRow("null", "the service stores no metadata configuration for an index created without one")]
    [DataRow("{}", "a configuration may declare no keys")]
    public async Task GetIndexAsync_ShouldReturnNoNonFilterableKeys_GivenNoKeysDeclared(string metadataConfiguration, string because)
    {
        this.Respond(200, IndexBody("\"cosine\"", "\"float32\"", metadataConfiguration));
        (await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1")).NonFilterableMetadataKeys
            .Should().BeEmpty(because);
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldReturnNoNonFilterableKeys_GivenNoMetadataConfiguration()
    {
        this.Respond(200, $"{{\"index\":{{\"indexName\":\"docs-v1\",\"dataType\":\"float32\",\"dimension\":1536,\"distanceMetric\":\"cosine\",\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":{CreationTime}}}}}");
        (await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1")).NonFilterableMetadataKeys
            .Should().BeEmpty("the collection is never null, whatever the service leaves out");
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldIgnoreFieldsItDoesNotKnow()
    {
        this.Respond(200, $"{{\"index\":{{\"indexName\":\"docs-v1\",\"indexArn\":\"arn:aws:s3vectors:docs-v1\",\"status\":\"ACTIVE\",\"dataType\":\"float32\",\"dimension\":1536,\"distanceMetric\":\"cosine\",\"metadataConfiguration\":null,\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":{CreationTime}}}}}");
        (await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1")).IndexName
            .Should().Be("docs-v1", "decoding ignores a field the SDK does not know (SDK principles)");
    }

    [TestMethod]
    [DataRow("null", "the service answered with no index")]
    [DataRow("{}", "the answer lacks the index")]
    [DataRow("{\"index\":{\"indexName\":\"docs-v1\"}}", "the index lacks its settings")]
    [DataRow("<html>", "the answer is not JSON")]
    public async Task GetIndexAsync_ShouldThrowStorageException_GivenAnUnreadableAnswer(string body, string because)
    {
        this.Respond(200, body);
        await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1"))
            .Should().ThrowAsync<SupabaseStorageException>(because);
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldNameTheOperation_GivenAnEmptyAnswer()
    {
        this.Respond(200, "null");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Message.Should().Be("The vector service returned an empty GetIndex response.");
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects a malformed index name")]
    [DataRow(404, "NotFoundException", "the bucket or the index does not exist")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task GetIndexAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1"))
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldClassifyAsNotFound_GivenAMissingIndex()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"NotFoundException\",\"message\":\"resource \\\"docs-v1\\\" not found\",\"code\":\"NotFoundException\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotFound);
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/GetIndex not found\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, IndexBody("\"cosine\"", "\"float32\"", "null"));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1", source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task GetIndexAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, IndexBody("\"cosine\"", "\"float32\"", "null"));
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1");
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    [TestMethod]
    public void GetIndexAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, IndexBody("\"cosine\"", "\"float32\"", "null"));
        BlockingCaller.Completes(() => this.client.Vectors.From("embeddings-prod").GetIndexAsync("docs-v1"))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string IndexBody(string distanceMetric, string dataType, string metadataConfiguration) =>
        $"{{\"index\":{{\"indexName\":\"docs-v1\",\"dataType\":{dataType},\"dimension\":1536,\"distanceMetric\":{distanceMetric},\"metadataConfiguration\":{metadataConfiguration},\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":{CreationTime}}}}}";

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
