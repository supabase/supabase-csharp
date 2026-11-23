using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.Analytics;

/// <summary>
/// Contract tests for deleting an analytics bucket: the request the client puts on the wire
/// (method, path, headers, no body) and how each response the Iceberg route can give is surfaced.
/// Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageAnalyticsApiContractTests
{
    private const string BucketPath = "/storage/v1/iceberg/bucket/analytics-data";
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
    public async Task DeleteBucketAsync_ShouldSendBodylessDeleteToIcebergBucketRoute()
    {
        this.Respond(BucketPath, 200, "{\"message\":\"Successfully deleted\"}");
        await this.client.Analytics.DeleteBucketAsync("analytics-data");
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("DELETE", "the Iceberg bucket route is DELETE /bucket/:bucketName");
            request.Path.Should().Be(BucketPath);
            request.Body.Should().BeNullOrEmpty("the route discards any request body");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldEscapeTheNameAsOneSegment()
    {
        this.Respond("/storage/v1/iceberg/bucket/*", 200, "{}");
        await this.client.Analytics.DeleteBucketAsync("a/b?c");
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith("/storage/v1/iceberg/bucket/a%2Fb%3Fc");
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldCompleteGivenEmptyBody()
    {
        this.Respond(BucketPath, 200, "");
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data");
        await act.Should().NotThrowAsync("the acknowledgement is discarded");
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(BucketPath, 200, "{}");
        var analytics = this.client.Analytics;
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await analytics.DeleteBucketAsync("analytics-data");
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldThrowNotFound_GivenMissingBucket()
    {
        this.Respond(BucketPath, 404, "{\"error\":{\"message\":\"Bucket not found\",\"type\":\"NoSuchBucket\",\"code\":404}}");
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason
            .Should().Be(FailureHint.Reason.NotFound);
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldReportBodyStatusCode_GivenBodyDiffersFromHttpStatus()
    {
        this.Respond(BucketPath, 400, "{\"statusCode\":\"404\",\"error\":\"not_found\",\"message\":\"Bucket not found\"}");
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode
            .Should().Be(404, "the statusCode the service puts in its body wins over the transport status");
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldReportHttpStatus_GivenBodyCarriesNoStatusCode()
    {
        this.Respond(BucketPath, 404, "{\"error\":{\"message\":\"Bucket not found\",\"type\":\"NoSuchBucket\",\"code\":404}}");
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode
            .Should().Be(404, "the Iceberg error envelope has no top-level statusCode");
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldThrowNotAuthorized_GivenUnauthorizedCaller()
    {
        this.Respond(BucketPath, 401, "{\"message\":\"Unauthorized\",\"statusCode\":\"401\"}");
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason
            .Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldThrowStorageException_GivenFeatureNotEnabled()
    {
        this.Respond(BucketPath, 403, "{\"error\":{\"message\":\"Iceberg buckets are not enabled for this tenant\",\"type\":\"FeatureNotEnabled\",\"code\":403}}");
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode.Should().Be(403);
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldThrowInternal_GivenServerFailure()
    {
        this.Respond(BucketPath, 500, "{\"error\":{\"message\":\"boom\",\"type\":\"InternalError\",\"code\":500}}");
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason
            .Should().Be(FailureHint.Reason.Internal);
    }

    [TestMethod]
    public async Task DeleteBucketAsync_ShouldCancelTheRequest_GivenCanceledToken()
    {
        this.Respond(BucketPath, 200, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.client.Analytics.DeleteBucketAsync("analytics-data", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
