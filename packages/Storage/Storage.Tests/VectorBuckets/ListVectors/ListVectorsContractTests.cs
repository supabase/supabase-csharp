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
using Supabase.Storage.Exceptions;
using Supabase.Storage.VectorBuckets;
using Supabase.Storage.VectorBuckets.Common;
using Supabase.Storage.VectorBuckets.ListVectors;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.VectorBuckets.ListVectors;

/// <summary>
/// Contract tests for <c>Vectors.From(bucket).Index(name).ListVectorsAsync</c>: the request it sends to
/// <c>POST /vector/ListVectors</c> and how each service answer maps to the caller's page.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListVectorsContractTests
{
    private const string Path = "/storage/v1/vector/ListVectors";
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
    public async Task ListVectorsAsync_ShouldPostTheBucketAndIndexToTheVectorRoute()
    {
        this.Respond(200, "{\"vectors\":[]}");
        await this.Index().ListVectorsAsync();
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
    public async Task ListVectorsAsync_ShouldReturnThePageAndTheNextPageToken()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\",\"data\":{\"float32\":[0.5,0.25]},\"metadata\":{\"title\":\"Intro\"}},{\"key\":\"doc-2\"}],\"nextToken\":\"doc-2\"}");
        var page = await this.Index().ListVectorsAsync(new ListVectorsOptions { Include = VectorContent.Data | VectorContent.Metadata });
        using (new AssertionScope())
        {
            page.Items.Select(vector => vector.Key).Should().Equal("doc-1", "doc-2");
            page.Items[0].Data!.Float32.Should().Equal(0.5f, 0.25f);
            page.Items[0].Metadata!["title"].GetString().Should().Be("Intro");
            page.Items[1].Should().Be(new StoredVector { Key = "doc-2" });
            page.NextPageToken.Should().Be("doc-2");
        }
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldReturnNoNextPageToken_GivenTheLastPage()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\"}]}");
        (await this.Index().ListVectorsAsync()).NextPageToken.Should().BeNull("the service leaves out the token on the last page");
    }

    [TestMethod]
    [DataRow("{\"vectors\":[]}", "the index holds no vector")]
    [DataRow("{}", "the service listed nothing")]
    public async Task ListVectorsAsync_ShouldReturnAnEmptyPage_GivenNoVectors(string body, string because)
    {
        this.Respond(200, body);
        (await this.Index().ListVectorsAsync()).Items.Should().BeEmpty(because);
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldIgnoreFieldsItDoesNotKnow()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\",\"distance\":0.1}],\"segment\":0}");
        (await this.Index().ListVectorsAsync()).Items.Should().ContainSingle().Which.Key.Should().Be("doc-1");
    }

    [TestMethod]
    [DataRow("null", "the service answered with no body")]
    [DataRow("{\"vectors\":[{\"data\":{\"float32\":[0.5]}}]}", "a vector lacks its key")]
    [DataRow("<html>", "the answer is not JSON")]
    public async Task ListVectorsAsync_ShouldThrowStorageException_GivenAnUnreadableAnswer(string body, string because)
    {
        this.Respond(200, body);
        await FluentActions.Awaiting(() => this.Index().ListVectorsAsync())
            .Should().ThrowAsync<SupabaseStorageException>(because);
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldNameTheOperation_GivenAnEmptyAnswer()
    {
        this.Respond(200, "null");
        (await FluentActions.Awaiting(() => this.Index().ListVectorsAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Message.Should().Be("The vector service returned an empty ListVectors response.");
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects an out-of-range page size or segment count")]
    [DataRow(400, "InvalidParameter", "the service rejects a segment index not below the segment count")]
    [DataRow(404, "NotFoundException", "the bucket or the index does not exist")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task ListVectorsAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.Index().ListVectorsAsync())
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldClassifyAsNotFound_GivenAMissingIndex()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"NotFoundException\",\"message\":\"resource \\\"docs-v1\\\" not found\",\"code\":\"NotFoundException\"}");
        (await FluentActions.Awaiting(() => this.Index().ListVectorsAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotFound);
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.Index().ListVectorsAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/ListVectors not found\"}");
        (await FluentActions.Awaiting(() => this.Index().ListVectorsAsync())
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, "{\"vectors\":[]}");
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.Index().ListVectorsAsync(null, source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, "{\"vectors\":[]}");
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.Index().ListVectorsAsync();
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    [TestMethod]
    public void ListVectorsAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, "{\"vectors\":[]}");
        BlockingCaller.Completes(() => this.Index().ListVectorsAsync())
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private IVectorIndex Index() => this.client.Vectors.From("embeddings-prod").Index("docs-v1");

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
