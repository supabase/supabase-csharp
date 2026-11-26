using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Common;
using Supabase.Storage.AnalyticsBuckets.ListBuckets;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.AnalyticsBuckets.ListBuckets;

/// <summary>
/// Contract tests for listing analytics buckets: the request the client puts on the wire (method, path, headers,
/// the query built from <see cref="ListAnalyticsBucketsOptions"/>) and how each response the Iceberg bucket route
/// can give is surfaced. Bodies are pinned from the storage route source (<c>GET /iceberg/bucket</c>), not observed
/// on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListAnalyticsBucketsContractTests
{
    private const string BucketPath = "/storage/v1/iceberg/bucket";
    private const string ListedBody = "[{\"id\":\"analytics-data\",\"name\":\"analytics-data\",\"created_at\":\"2026-11-24T10:00:00.000Z\",\"updated_at\":\"2026-11-25T11:30:00.000Z\"},{\"id\":\"events\",\"name\":\"events\",\"created_at\":\"2026-11-24T10:00:00.000Z\",\"updated_at\":\"2026-11-24T10:00:00.000Z\"}]";
    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/storage/v1", new Dictionary<string, string>
        {
            { "Authorization", "Bearer service-key" }
        });
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    public async Task ListBucketsAsync_ShouldSendBodylessGetToIcebergBucketRoute()
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync();
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("GET", "the Iceberg bucket route lists with GET /bucket");
            request.Path.Should().Be(BucketPath);
            request.Body.Should().BeNullOrEmpty("the options travel in the query string");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldSendNoQuery_GivenNoOptions()
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync();
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query
            .Should().BeNullOrEmpty("the service owns the defaults: every bucket, sorted by name ascending");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldSendNoQuery_GivenEmptyOptions()
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions());
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query
            .Should().BeNullOrEmpty("an option left unset is not sent");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldSendEveryOptionAsQueryParameter()
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions
        {
            Limit = 10,
            Offset = 20,
            Sort = new AnalyticsBucketSort(AnalyticsBucketSortColumn.CreatedAt, ListSortDirection.Descending),
            Search = "analytics",
        });
        this.SentQuery().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            { "limit", "10" },
            { "offset", "20" },
            { "sortColumn", "created_at" },
            { "sortOrder", "desc" },
            { "search", "analytics" },
        }, "the route reads limit, offset, sortColumn, sortOrder and search from the query string");
    }

    [TestMethod]
    [DataRow(AnalyticsBucketSortColumn.Name, "name")]
    [DataRow(AnalyticsBucketSortColumn.CreatedAt, "created_at")]
    [DataRow(AnalyticsBucketSortColumn.UpdatedAt, "updated_at")]
    public async Task ListBucketsAsync_ShouldSendTheWireNameOfTheSortColumn(AnalyticsBucketSortColumn column, string expected)
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions { Sort = new AnalyticsBucketSort(column) });
        this.SentQuery()["sortColumn"].Should().Be(expected, "the route's sortColumn enum is the snake_case column name");
    }

    [TestMethod]
    [DataRow(ListSortDirection.Ascending, "asc")]
    [DataRow(ListSortDirection.Descending, "desc")]
    public async Task ListBucketsAsync_ShouldSendTheWireNameOfTheSortDirection(ListSortDirection direction, string expected)
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions { Sort = new AnalyticsBucketSort(AnalyticsBucketSortColumn.Name, direction) });
        this.SentQuery()["sortOrder"].Should().Be(expected, "the route's sortOrder enum is asc or desc");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldSortAscending_GivenSortWithoutDirection()
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions { Sort = new AnalyticsBucketSort(AnalyticsBucketSortColumn.UpdatedAt) });
        this.SentQuery()["sortOrder"].Should().Be("asc", "a sort defaults to ascending");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldEscapeTheSearchTerm()
    {
        this.Respond(200, ListedBody);
        await this.client.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions { Search = "a&b=c d+e" });
        this.SentQuery()["search"].Should().Be("a&b=c d+e", "the search term reaches the service as one query value");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldReturnTheListedBuckets()
    {
        this.Respond(200, ListedBody);
        var buckets = await this.client.Analytics.ListBucketsAsync();
        buckets.Should().BeEquivalentTo(new[]
        {
            new AnalyticsBucket
            {
                Name = "analytics-data",
                CreatedAt = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 11, 25, 11, 30, 0, TimeSpan.Zero),
            },
            new AnalyticsBucket
            {
                Name = "events",
                CreatedAt = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero),
            },
        }, options => options.WithStrictOrdering(), "the service's order is kept");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldReturnEmptyList_GivenNoBuckets()
    {
        this.Respond(200, "[]");
        (await this.client.Analytics.ListBucketsAsync()).Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldReturnEmptyList_GivenNullBody()
    {
        this.Respond(200, "null");
        (await this.client.Analytics.ListBucketsAsync()).Should().BeEmpty("a returned collection is never null");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldThrowStorageException_GivenEmptySuccessBody()
    {
        this.Respond(200, "");
        var act = () => this.client.Analytics.ListBucketsAsync();
        await act.Should().ThrowAsync<SupabaseStorageException>("a success without a JSON array is not a listing");
    }

    [TestMethod]
    public void ListBucketsAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, ListedBody);
        BlockingCaller.Completes(() => this.client.Analytics.ListBucketsAsync())
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(200, ListedBody);
        var analytics = this.client.Analytics;
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await analytics.ListBucketsAsync();
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldThrowStorageException_GivenRejectedPagination()
    {
        this.Respond(400, "{\"statusCode\":\"400\",\"error\":\"Bad Request\",\"message\":\"querystring/limit must be >= 1\"}");
        var act = () => this.client.Analytics.ListBucketsAsync(new ListAnalyticsBucketsOptions { Limit = 0 });
        (await act.Should().ThrowAsync<SupabaseStorageException>("the service validates the pagination range")).Which.StatusCode
            .Should().Be(400);
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldThrowNotAuthorized_GivenUnauthorizedCaller()
    {
        this.Respond(401, "{\"message\":\"Unauthorized\",\"statusCode\":\"401\"}");
        var act = () => this.client.Analytics.ListBucketsAsync();
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason
            .Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldThrowStorageException_GivenFeatureNotEnabled()
    {
        this.Respond(403, "{\"error\":{\"message\":\"Iceberg buckets are not enabled for this tenant\",\"type\":\"FeatureNotEnabled\",\"code\":403}}");
        var act = () => this.client.Analytics.ListBucketsAsync();
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode.Should().Be(403);
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldThrowInternal_GivenServerFailure()
    {
        this.Respond(500, "{\"error\":{\"message\":\"boom\",\"type\":\"InternalError\",\"code\":500}}");
        var act = () => this.client.Analytics.ListBucketsAsync();
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason
            .Should().Be(FailureHint.Reason.Internal);
    }

    [TestMethod]
    public async Task ListBucketsAsync_ShouldCancelTheRequest_GivenCanceledToken()
    {
        this.Respond(200, ListedBody);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.client.Analytics.ListBucketsAsync(cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    private IDictionary<string, string> SentQuery()
    {
        var query = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query!;
        var sent = new Dictionary<string, string>();
        foreach (var parameter in query)
            sent[parameter.Key] = parameter.Value.ToString();
        return sent;
    }

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(BucketPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
