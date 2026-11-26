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

namespace Storage.Tests.FileBuckets.DeleteBucketLifecycle;

/// <summary>
/// Contract tests for <c>DeleteBucketLifecycleAsync</c>: the request it sends to
/// <c>DELETE /bucket/{id}/lifecycle</c> and how the route's answers reach the caller.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteBucketLifecycleContractTests
{
    private const string Path = "/storage/v1/bucket/avatars/lifecycle";
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
    public async Task DeleteBucketLifecycleAsync_ShouldSendBodylessDeleteToTheLifecycleRoute()
    {
        this.Respond(200, "{\"message\":\"Successfully deleted\"}");
        await this.client.DeleteBucketLifecycleAsync("avatars");
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("DELETE", "the lifecycle route is DELETE /bucket/:bucketId/lifecycle");
            request.Path.Should().Be(Path);
            request.Body.Should().BeNullOrEmpty("the route takes no body");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task DeleteBucketLifecycleAsync_ShouldThrowWithTheServiceCode_GivenLifecycleDisabled()
    {
        this.Respond(400, "{\"statusCode\":\"409\",\"code\":\"FeatureNotEnabled\",\"error\":\"FeatureNotEnabled\",\"message\":\"The feature object lifecycles is not enabled for this resource\"}");
        var act = () => this.client.DeleteBucketLifecycleAsync("avatars");
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Code.Should().Be("FeatureNotEnabled", "lifecycle support must be enabled on the Storage API");
            exception.StatusCode.Should().Be(409, "the statusCode the service puts in its body wins over the transport status");
        }
    }

    [TestMethod]
    public async Task DeleteBucketLifecycleAsync_ShouldCancelTheRequest_GivenCanceledToken()
    {
        this.Respond(200, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.client.DeleteBucketLifecycleAsync("avatars", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
