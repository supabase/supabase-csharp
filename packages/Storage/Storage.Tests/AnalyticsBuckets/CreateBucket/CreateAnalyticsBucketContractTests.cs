using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Storage.Tests.Support;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.AnalyticsBuckets.CreateBucket;

/// <summary>
/// Contract tests for creating an analytics bucket: the request the client puts on the wire and how each
/// response the Iceberg bucket route can give is surfaced. Failure bodies are pinned from the storage route
/// source (<c>POST /iceberg/bucket</c>), not observed on a running service. A 409 for a duplicate name and a
/// 400 for an invalid name are not confirmed from that source and are not pinned here.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateAnalyticsBucketContractTests
{
    private const string BucketPath = "/storage/v1/iceberg/bucket";
    private const string CreatedBody = "{\"id\":\"analytics-data\",\"name\":\"analytics-data\",\"created_at\":\"2026-11-24T10:00:00.000Z\",\"updated_at\":\"2026-11-25T11:30:00.000Z\"}";
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
    public async Task CreateBucketAsync_ShouldPostJsonToIcebergBucketRoute()
    {
        this.Respond(200, CreatedBody);
        await this.client.Analytics.CreateBucketAsync("analytics-data");
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST", "the Iceberg bucket route is POST /bucket");
            request.Path.Should().Be(BucketPath);
            request.Headers!["Content-Type"].Should().ContainSingle().Which.Should().StartWith("application/json");
            request.Headers["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldReturnTheCreatedBucket()
    {
        this.Respond(200, CreatedBody);
        var bucket = await this.client.Analytics.CreateBucketAsync("analytics-data");
        using (new AssertionScope())
        {
            bucket.Name.Should().Be("analytics-data");
            bucket.CreatedAt.Should().Be(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));
            bucket.UpdatedAt.Should().Be(new DateTimeOffset(2026, 11, 25, 11, 30, 0, TimeSpan.Zero));
        }
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldThrowStorageException_GivenEmptySuccessBody()
    {
        this.Respond(200, "");
        var act = () => this.client.Analytics.CreateBucketAsync("analytics-data");
        await act.Should().ThrowAsync<SupabaseStorageException>("a success without the created bucket cannot be returned");
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldReturnEmptyName_GivenBodyWithoutName()
    {
        this.Respond(200, "{}");
        (await this.client.Analytics.CreateBucketAsync("analytics-data")).Name
            .Should().BeEmpty("a member the service omits stays at its non-null default");
    }

    [TestMethod]
    public void CreateBucketAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, CreatedBody);
        BlockingCaller.Completes(() => this.client.Analytics.CreateBucketAsync("analytics-data"))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(200, CreatedBody);
        var analytics = this.client.Analytics;
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await analytics.CreateBucketAsync("analytics-data");
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldThrowNotAuthorized_GivenUnauthorizedCaller()
    {
        this.Respond(401, "{\"message\":\"Unauthorized\",\"statusCode\":\"401\"}");
        var act = () => this.client.Analytics.CreateBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason
            .Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldThrowStorageException_GivenFeatureNotEnabled()
    {
        this.Respond(403, "{\"error\":{\"message\":\"Iceberg buckets are not enabled for this tenant\",\"type\":\"FeatureNotEnabled\",\"code\":403}}");
        var act = () => this.client.Analytics.CreateBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode.Should().Be(403);
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldReportHttpStatus_GivenBodyCarriesNoStatusCode()
    {
        this.Respond(404, "{\"error\":{\"message\":\"Not found\",\"type\":\"NotFound\",\"code\":404}}");
        var act = () => this.client.Analytics.CreateBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode
            .Should().Be(404, "the Iceberg error envelope has no top-level statusCode");
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldThrowInternal_GivenServerFailure()
    {
        this.Respond(500, "{\"error\":{\"message\":\"boom\",\"type\":\"InternalError\",\"code\":500}}");
        var act = () => this.client.Analytics.CreateBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason
            .Should().Be(FailureHint.Reason.Internal);
    }

    [TestMethod]
    public async Task CreateBucketAsync_ShouldCancelTheRequest_GivenCanceledToken()
    {
        this.Respond(200, CreatedBody);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.client.Analytics.CreateBucketAsync("analytics-data", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(BucketPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
