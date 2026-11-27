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
using Supabase.Storage.VectorBuckets.GetBucket;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.VectorBuckets.GetBucket;

/// <summary>
/// Contract tests for <c>Vectors.GetVectorBucketAsync</c>: the request it sends to
/// <c>POST /vector/GetVectorBucket</c> and how each service answer maps to the caller's result.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetVectorBucketContractTests
{
    private const string Path = "/storage/v1/vector/GetVectorBucket";
    private const long CreationTime = 1795590525;
    private const string PlainBucket = "{\"vectorBucket\":{\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":1795590525}}";

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
    public async Task GetVectorBucketAsync_ShouldPostTheBucketNameToTheVectorRoute()
    {
        this.Respond(200, PlainBucket);
        await this.client.Vectors.GetVectorBucketAsync("embeddings-prod");
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
    public async Task GetVectorBucketAsync_ShouldReturnTheBucketDetails_GivenNoEncryption()
    {
        this.Respond(200, PlainBucket);
        (await this.client.Vectors.GetVectorBucketAsync("embeddings-prod")).Should().BeEquivalentTo(new VectorBucketDetails
        {
            VectorBucketName = "embeddings-prod",
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(CreationTime),
        }, "the service describes the bucket with a creation time in Unix seconds and no encryption when it reports none");
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldReturnTheEncryption_GivenTheServiceReportsIt()
    {
        this.Respond(200, "{\"vectorBucket\":{\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":1795590525,\"encryptionConfiguration\":{\"sseType\":\"aws:kms\",\"kmsKeyArn\":\"arn:aws:kms:key\"}}}");
        (await this.client.Vectors.GetVectorBucketAsync("embeddings-prod")).Encryption
            .Should().BeEquivalentTo(new VectorBucketEncryption { SseType = "aws:kms", KmsKeyArn = "arn:aws:kms:key" });
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldReturnPartialEncryption_GivenTheServiceReportsOnlyTheType()
    {
        this.Respond(200, "{\"vectorBucket\":{\"vectorBucketName\":\"embeddings-prod\",\"creationTime\":1795590525,\"encryptionConfiguration\":{\"sseType\":\"AES256\"}}}");
        (await this.client.Vectors.GetVectorBucketAsync("embeddings-prod")).Encryption
            .Should().BeEquivalentTo(new VectorBucketEncryption { SseType = "AES256" }, "the key is only reported for KMS encryption");
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldIgnoreFieldsItDoesNotKnow()
    {
        this.Respond(200, "{\"vectorBucket\":{\"vectorBucketName\":\"embeddings-prod\",\"vectorBucketArn\":\"arn:aws:s3vectors:embeddings-prod\",\"creationTime\":1795590525}}");
        (await this.client.Vectors.GetVectorBucketAsync("embeddings-prod")).VectorBucketName
            .Should().Be("embeddings-prod", "decoding ignores a field the SDK does not know (SDK principles)");
    }

    [TestMethod]
    [DataRow("null", "the service answered with no bucket")]
    [DataRow("{}", "the answer lacks the bucket")]
    [DataRow("{\"vectorBucket\":{\"vectorBucketName\":\"embeddings-prod\"}}", "the bucket lacks its creation time")]
    [DataRow("<html>", "the answer is not JSON")]
    public async Task GetVectorBucketAsync_ShouldThrowStorageException_GivenAnUnreadableAnswer(string body, string because)
    {
        this.Respond(200, body);
        await FluentActions.Awaiting(() => this.client.Vectors.GetVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>(because);
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldNameTheOperation_GivenAnEmptyAnswer()
    {
        this.Respond(200, "null");
        (await FluentActions.Awaiting(() => this.client.Vectors.GetVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Message.Should().Be("The vector service returned an empty GetVectorBucket response.");
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects a malformed bucket name")]
    [DataRow(404, "NotFoundException", "the bucket does not exist")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task GetVectorBucketAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.client.Vectors.GetVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldClassifyAsNotFound_GivenAMissingBucket()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"NotFoundException\",\"message\":\"resource \\\"embeddings-prod\\\" not found\",\"code\":\"NotFoundException\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.GetVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotFound);
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.client.Vectors.GetVectorBucketAsync("embeddings-prod"))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, PlainBucket);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.client.Vectors.GetVectorBucketAsync("embeddings-prod", source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task GetVectorBucketAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, PlainBucket);
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.client.Vectors.GetVectorBucketAsync("embeddings-prod");
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    [TestMethod]
    public void GetVectorBucketAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, PlainBucket);
        BlockingCaller.Completes(() => this.client.Vectors.GetVectorBucketAsync("embeddings-prod"))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
