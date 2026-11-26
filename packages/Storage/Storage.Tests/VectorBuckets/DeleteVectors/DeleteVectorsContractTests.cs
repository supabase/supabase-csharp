using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using Supabase.Storage.VectorBuckets;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.VectorBuckets.DeleteVectors;

/// <summary>
/// Contract tests for <c>Vectors.From(bucket).Index(name).DeleteVectorsAsync</c>: the request it sends to
/// <c>POST /vector/DeleteVectors</c> and how each service answer maps to the caller's result.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteVectorsContractTests
{
    private const string Path = "/storage/v1/vector/DeleteVectors";
    private static readonly string[] Keys = { "doc-1", "doc-2" };
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
    public async Task DeleteVectorsAsync_ShouldPostTheBucketIndexAndKeysToTheVectorRoute()
    {
        this.Respond(200, string.Empty);
        await this.Index().DeleteVectorsAsync(Keys);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("{\"vectorBucketName\":\"embeddings-prod\",\"indexName\":\"docs-v1\",\"keys\":[\"doc-1\",\"doc-2\"]}");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldLeaveTheJudgmentToTheService_GivenAnEmptyCollection()
    {
        this.Respond(200, string.Empty);
        await this.Index().DeleteVectorsAsync(Array.Empty<string>());
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body.Should().Contain("\"keys\":[]",
            "validation belongs to the service: https://github.com/supabase/sdk/blob/main/docs/principles.md#validation-belongs-to-the-service");
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects an empty batch, an empty key or too many keys")]
    [DataRow(404, "NotFoundException", "the bucket or the index does not exist")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task DeleteVectorsAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.Index().DeleteVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.Index().DeleteVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/DeleteVectors not found\"}");
        (await FluentActions.Awaiting(() => this.Index().DeleteVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, string.Empty);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.Index().DeleteVectorsAsync(Keys, source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task DeleteVectorsAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, string.Empty);
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.Index().DeleteVectorsAsync(Keys);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    private IVectorIndex Index() => this.client.Vectors.From("embeddings-prod").Index("docs-v1");

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
