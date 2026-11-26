using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using Supabase.Storage.VectorBuckets;
using Supabase.Storage.VectorBuckets.ListVectors;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.VectorBuckets.ListVectors;

/// <summary>
/// Contract tests for splitting a full scan of an index across parallel workers: each worker's request carries its own
/// segment next to its own paging token, and the service's answer to an invalid segment becomes the caller's exception.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ParallelVectorScanContractTests
{
    private const string Path = "/storage/v1/vector/ListVectors";
    private const int SegmentCount = 4;
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
    public async Task ListVectorsAsync_ShouldSendEachWorkersSegmentAndToken_GivenParallelScans()
    {
        this.Respond(200, "{\"vectors\":[]}");
        await Task.WhenAll(Enumerable.Range(0, SegmentCount).Select(segmentIndex => this.Index().ListVectorsAsync(
            new ListVectorsOptions { Segment = new VectorSegment(segmentIndex, SegmentCount), NextToken = $"token-{segmentIndex}" })));
        this.server.LogEntries.Select(entry => entry.RequestMessage!.Body).Should().BeEquivalentTo(
            Enumerable.Range(0, SegmentCount).Select(segmentIndex =>
                $"{{\"vectorBucketName\":\"embeddings-prod\",\"indexName\":\"docs-v1\",\"nextToken\":\"token-{segmentIndex}\",\"segmentCount\":{SegmentCount},\"segmentIndex\":{segmentIndex}}}"),
            "every parallel call carries both segment numbers and the token of its own segment");
    }

    [TestMethod]
    public async Task ListVectorsAsync_ShouldLeaveTheSegmentRangeToTheService_GivenASegmentCountAbove16()
    {
        this.Respond(400, "{\"statusCode\":\"400\",\"error\":\"Error\",\"message\":\"body/segmentCount must be <= 16\",\"code\":\"InvalidRequest\"}");
        var thrown = await FluentActions.Awaiting(() => this.Index().ListVectorsAsync(new ListVectorsOptions { Segment = new VectorSegment(0, 17) }))
            .Should().ThrowAsync<SupabaseStorageException>("the service owns the segment range, which the SDK does not check");
        using (new AssertionScope())
        {
            thrown.Which.Code.Should().Be("InvalidRequest");
            thrown.Which.Reason.Should().Be(FailureHint.Reason.InvalidInput);
            this.server.LogEntries.Single().RequestMessage!.Body.Should().Contain("\"segmentCount\":17");
        }
    }

    private IVectorIndex Index() => this.client.Vectors.From("embeddings-prod").Index("docs-v1");

    private void Respond(int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));
}
