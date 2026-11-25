using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
using Supabase.Storage.QueryVectors;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.QueryVectors;

/// <summary>
/// Contract tests for <c>Vectors.From(bucket).Index(name).QueryVectorsAsync</c>: the request it sends to
/// <c>POST /vector/QueryVectors</c> and how each service answer maps to the caller's result.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class QueryVectorsContractTests
{
    private const string Path = "/storage/v1/vector/QueryVectors";
    private const string FullBody = "{\"vectors\":[{\"key\":\"doc-1\",\"distance\":0.25,\"metadata\":{\"title\":\"Intro\"}},{\"key\":\"doc-2\",\"distance\":0.5,\"metadata\":{}}],\"distanceMetric\":\"cosine\",\"nextToken\":\"next\"}";
    private static readonly float[] Query = { 0.5f, 0.25f };

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
    public async Task QueryVectorsAsync_ShouldPostOnlyTheRequiredFields_GivenNoOptions()
    {
        this.Respond(200, FullBody);
        await this.Index().QueryVectorsAsync(Query, 5);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("{\"vectorBucketName\":\"embeddings-prod\",\"indexName\":\"docs-v1\",\"queryVector\":{\"float32\":[0.5,0.25]},\"topK\":5}", "an unset option is left to the service's default");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldSendTheFilterTokenAndContentFlags_GivenOptions()
    {
        this.Respond(200, FullBody);
        var options = new QueryVectorsOptions
        {
            Filter = JsonDocument.Parse("{\"kind\":\"x\"}").RootElement,
            NextToken = "token",
            Include = VectorMatchContent.Distance | VectorMatchContent.Metadata,
        };
        await this.Index().QueryVectorsAsync(Query, 5, options);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body
            .Should().Be("{\"vectorBucketName\":\"embeddings-prod\",\"indexName\":\"docs-v1\",\"queryVector\":{\"float32\":[0.5,0.25]},\"topK\":5,\"filter\":{\"kind\":\"x\"},\"nextToken\":\"token\",\"returnDistance\":true,\"returnMetadata\":true}");
    }

    [TestMethod]
    [DataRow(VectorMatchContent.Distance, "\"returnDistance\":true", "\"returnMetadata\"")]
    [DataRow(VectorMatchContent.Metadata, "\"returnMetadata\":true", "\"returnDistance\"")]
    public async Task QueryVectorsAsync_ShouldAskOnlyForTheChosenContent(VectorMatchContent include, string sent, string omitted)
    {
        this.Respond(200, FullBody);
        await this.Index().QueryVectorsAsync(Query, 5, new QueryVectorsOptions { Include = include });
        var body = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body;
        using (new AssertionScope())
        {
            body.Should().Contain(sent);
            body.Should().NotContain(omitted, "the service owns the default of the flag that was not asked for");
        }
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldReturnTheMatches()
    {
        this.Respond(200, FullBody);
        var result = await this.Index().QueryVectorsAsync(Query, 5);
        using (new AssertionScope())
        {
            result.Matches.Select(match => (match.Key, match.Distance)).Should().Equal(new[] { ("doc-1", (float?) 0.25f), ("doc-2", 0.5f) }, "the service lists the nearest vectors first");
            result.Matches[0].Metadata!["title"].GetString().Should().Be("Intro");
            result.Matches[1].Metadata.Should().BeEmpty("the vector has no metadata");
            result.DistanceMetric.Should().Be(VectorDistanceMetric.Cosine);
            result.NextToken.Should().Be("next");
        }
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldReturnOnlyTheKeys_GivenTheServiceOmitsDistanceAndMetadata()
    {
        this.Respond(200, "{\"vectors\":[{\"key\":\"doc-1\"}],\"distanceMetric\":\"euclidean\"}");
        var result = await this.Index().QueryVectorsAsync(Query, 5);
        using (new AssertionScope())
        {
            result.Matches.Should().BeEquivalentTo(new[] { new VectorMatch { Key = "doc-1" } });
            result.DistanceMetric.Should().Be(VectorDistanceMetric.Euclidean);
            result.NextToken.Should().BeNull("the service omits the token when no match remains");
        }
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldKeepTheNameOfAMetricItDoesNotKnow()
    {
        this.Respond(200, "{\"vectors\":[],\"distanceMetric\":\"dotproduct\"}");
        (await this.Index().QueryVectorsAsync(Query, 5)).DistanceMetric!.ToString()
            .Should().Be("dotproduct", "a metric the service adds later must still decode");
    }

    [TestMethod]
    [DataRow("{\"vectors\":[]}", "no vector is close enough")]
    [DataRow("{}", "the service left the list out")]
    public async Task QueryVectorsAsync_ShouldReturnNoMatchAndNoMetric_GivenNoVectorListed(string body, string because)
    {
        this.Respond(200, body);
        var result = await this.Index().QueryVectorsAsync(Query, 5);
        using (new AssertionScope())
        {
            result.Matches.Should().BeEmpty(because);
            result.DistanceMetric.Should().BeNull("the service reported none");
        }
    }

    [TestMethod]
    [DataRow("null", "the service answered with no result")]
    [DataRow("{\"vectors\":[{\"distance\":1}]}", "a match lacks its key")]
    [DataRow("<html>", "the answer is not JSON")]
    public async Task QueryVectorsAsync_ShouldThrowStorageException_GivenAnUnreadableAnswer(string body, string because)
    {
        this.Respond(200, body);
        await FluentActions.Awaiting(() => this.Index().QueryVectorsAsync(Query, 5))
            .Should().ThrowAsync<SupabaseStorageException>(because);
    }

    [TestMethod]
    [DataRow(400, "InvalidRequest", "the service rejects a topK out of range or a malformed body")]
    [DataRow(400, "InvalidParameter", "the service rejects a nextToken it cannot use or a topK above its limit")]
    [DataRow(404, "NotFoundException", "the bucket or the index does not exist")]
    [DataRow(500, "InternalError", "the service failed internally")]
    public async Task QueryVectorsAsync_ShouldThrowStorageException_GivenAServiceError(int status, string code, string because)
    {
        this.Respond(status, $"{{\"statusCode\":\"{status}\",\"code\":\"{code}\",\"message\":\"refused\"}}");
        var thrown = await FluentActions.Awaiting(() => this.Index().QueryVectorsAsync(Query, 5))
            .Should().ThrowAsync<SupabaseStorageException>(because);
        using (new AssertionScope())
        {
            thrown.Which.StatusCode.Should().Be(status);
            thrown.Which.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldClassifyAsNotAuthorized_GivenANonServiceRoleCaller()
    {
        this.Respond(403, "{\"statusCode\":\"403\",\"error\":\"Unauthorized\",\"message\":\"Invalid compact JWS\"}");
        (await FluentActions.Awaiting(() => this.Index().QueryVectorsAsync(Query, 5))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldThrowStorageException_GivenTheVectorServiceIsNotEnabled()
    {
        this.Respond(404, "{\"statusCode\":\"404\",\"error\":\"Not Found\",\"message\":\"Route POST:/vector/QueryVectors not found\"}");
        (await FluentActions.Awaiting(() => this.Index().QueryVectorsAsync(Query, 5))
            .Should().ThrowAsync<SupabaseStorageException>())
            .Which.StatusCode.Should().Be(404);
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldCancel_GivenACanceledToken()
    {
        this.Respond(200, FullBody);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await FluentActions.Awaiting(() => this.Index().QueryVectorsAsync(Query, 5, cancellationToken: source.Token))
            .Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public async Task QueryVectorsAsync_ShouldSendDynamicHeaders_GivenGetHeadersOnTheClient()
    {
        this.Respond(200, FullBody);
        this.client.GetHeaders = () => new Dictionary<string, string> { { "X-Dynamic", "fresh" } };
        await this.Index().QueryVectorsAsync(Query, 5);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Dynamic"]
            .Should().ContainSingle().Which.Should().Be("fresh");
    }

    [TestMethod]
    public void QueryVectorsAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, FullBody);
        BlockingCaller.Completes(() => this.Index().QueryVectorsAsync(Query, 5))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private IVectorIndex Index() => this.client.Vectors.From("embeddings-prod").Index("docs-v1");

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
