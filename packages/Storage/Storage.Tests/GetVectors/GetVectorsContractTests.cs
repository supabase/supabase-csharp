using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.GetVectors;

/// <summary>
/// Contract tests for <c>Vectors.From(bucket).Index(name).GetVectorsAsync</c>: the request it sends to
/// <c>POST /vector/GetVectors</c> and how each service answer maps to the caller's result.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetVectorsContractTests
{
    private const string Path = "/storage/v1/vector/GetVectors";
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
    public async Task GetVectorsAsync_ShouldPostTheBucketIndexAndKeysToTheVectorRoute()
    {
        this.Respond(200, "{\"vectors\":[]}");
        await this.Index().GetVectorsAsync(Keys);
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
    public async Task GetVectorsAsync_ShouldLeaveTheJudgmentToTheService_GivenAnEmptyCollection()
    {
        this.Respond(200, "{\"vectors\":[]}");
        await this.Index().GetVectorsAsync(Array.Empty<string>());
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body.Should().Contain("\"keys\":[]",
            "validation belongs to the service: https://github.com/supabase/sdk/blob/main/docs/principles.md#validation-belongs-to-the-service");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldReturnTheKeyDataAndMetadataOfEachVector()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\",\"data\":{\"float32\":[0.5,0.25]},\"metadata\":{\"title\":\"Intro\",\"page\":3,\"tags\":[\"a\",\"b\"]}}]}");
        var vector = (await this.Index().GetVectorsAsync(Keys, VectorContent.Data | VectorContent.Metadata)).Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            vector.Key.Should().Be("doc-1");
            vector.Data.Should().Equal(0.5f, 0.25f);
            vector.Metadata!["title"].GetString().Should().Be("Intro");
            vector.Metadata["page"].GetInt32().Should().Be(3);
            vector.Metadata["tags"].EnumerateArray().Select(tag => tag.GetString()).Should().Equal("a", "b");
        }
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldReturnEveryVectorInTheServiceOrder()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-2\"},{\"key\":\"doc-1\"}]}");
        (await this.Index().GetVectorsAsync(Keys)).Select(vector => vector.Key).Should().Equal("doc-2", "doc-1");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldLeaveDataAndMetadataNull_GivenTheServiceReturnedNeither()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\"}]}");
        (await this.Index().GetVectorsAsync(Keys)).Should().ContainSingle()
            .Which.Should().Be(new StoredVector { Key = "doc-1" }, "data and metadata are absent when the call did not ask for them");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldReturnEmptyMetadata_GivenAVectorWithNone()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\",\"metadata\":{}}]}");
        (await this.Index().GetVectorsAsync(Keys, VectorContent.Metadata)).Should().ContainSingle()
            .Which.Metadata.Should().NotBeNull().And.BeEmpty("asked-for metadata the vector does not have differs from metadata not asked for");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldLeaveDataNull_GivenADataTypeItDoesNotKnow()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\",\"data\":{\"float16\":[0.5,0.25]}}]}");
        (await this.Index().GetVectorsAsync(Keys, VectorContent.Data)).Should().ContainSingle()
            .Which.Data.Should().BeNull("decoding is open to new values: https://github.com/supabase/sdk/blob/main/docs/principles.md#decoding-is-open-to-new-values");
    }

    [TestMethod]
    [DataRow("{\"vectors\":[]}", "the service leaves out keys that match no vector")]
    [DataRow("{}", "an answer without vectors lists none, as for list_vectors and list_indexes")]
    public async Task GetVectorsAsync_ShouldReturnNoVectors_GivenNoVectorInTheAnswer(string body, string because)
    {
        this.Respond(200, body);
        (await this.Index().GetVectorsAsync(Keys)).Should().BeEmpty(because);
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldIgnoreFieldsItDoesNotKnow()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\",\"distance\":0.1}],\"nextToken\":\"t\"}");
        (await this.Index().GetVectorsAsync(Keys)).Should().ContainSingle().Which.Key.Should().Be("doc-1");
    }

    [TestMethod]
    [DataRow("null", "the service answered with no body")]
    [DataRow("", "the service answered with an empty body")]
    [DataRow("{\"vectors\":[{\"data\":{\"float32\":[0.5]}}]}", "a vector lacks its key")]
    [DataRow("<html>", "the answer is not JSON")]
    public async Task GetVectorsAsync_ShouldThrowStorageException_GivenAnUnreadableAnswer(string body, string because)
    {
        this.Respond(200, body);
        await FluentActions.Awaiting(() => this.Index().GetVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>(because);
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldNameTheOperation_GivenAnEmptyAnswer()
    {
        this.Respond(200, "null");
        (await FluentActions.Awaiting(() => this.Index().GetVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Message.Should().Be("The vector service returned an empty GetVectors response.");
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects an empty batch, an empty key or too many keys")]
    [DataRow(404, "NotFoundException", "the bucket or the index does not exist")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task GetVectorsAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.Index().GetVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldClassifyAsNotFound_GivenAMissingIndex()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"NotFoundException\",\"message\":\"resource \\\"docs-v1\\\" not found\",\"code\":\"NotFoundException\"}");
        (await FluentActions.Awaiting(() => this.Index().GetVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotFound);
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.Index().GetVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/GetVectors not found\"}");
        (await FluentActions.Awaiting(() => this.Index().GetVectorsAsync(Keys))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, "{\"vectors\":[]}");
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.Index().GetVectorsAsync(Keys, VectorContent.None, source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task GetVectorsAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, "{\"vectors\":[]}");
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.Index().GetVectorsAsync(Keys);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    [TestMethod]
    public void GetVectorsAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, "{\"vectors\":[]}");
        BlockingCaller.Completes(() => this.Index().GetVectorsAsync(Keys))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private IVectorIndex Index() => this.client.Vectors.From("embeddings-prod").Index("docs-v1");

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
