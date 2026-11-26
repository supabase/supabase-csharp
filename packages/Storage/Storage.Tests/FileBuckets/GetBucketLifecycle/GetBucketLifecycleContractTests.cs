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
using Supabase.Storage.FileBuckets.Common;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.FileBuckets.GetBucketLifecycle;

/// <summary>
/// Contract tests for <c>GetBucketLifecycleAsync</c>: the request it sends to
/// <c>GET /bucket/{id}/lifecycle</c> and how the route's answers reach the caller.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetBucketLifecycleContractTests
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
    public async Task GetBucketLifecycleAsync_ShouldSendBodylessGetToTheLifecycleRoute()
    {
        this.Respond(200, """{"rules":[]}""");
        await this.client.GetBucketLifecycleAsync("avatars");
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("GET");
            request.Path.Should().Be(Path);
            request.Body.Should().BeNullOrEmpty();
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task GetBucketLifecycleAsync_ShouldReturnTheStoredRules()
    {
        this.Respond(200, """
            {"rules":[
              {"id":"keep-two","status":"Enabled","filter":{},"noncurrentVersionExpiration":{"noncurrentDays":30,"newerNoncurrentVersions":2}},
              {"id":"rule-9f2c","status":"Disabled","filter":{},"noncurrentVersionExpiration":{"noncurrentDays":7}}]}
            """);
        (await this.client.GetBucketLifecycleAsync("avatars")).Should().BeEquivalentTo(new BucketLifecycle
        {
            Rules = new[]
            {
                new LifecycleRule
                {
                    Id = "keep-two",
                    Status = LifecycleRuleStatus.Enabled,
                    NoncurrentVersionExpiration = new NoncurrentVersionExpiration { NoncurrentDays = 30, NewerNoncurrentVersions = 2 },
                },
                new LifecycleRule
                {
                    Id = "rule-9f2c",
                    Status = LifecycleRuleStatus.Disabled,
                    NoncurrentVersionExpiration = new NoncurrentVersionExpiration { NoncurrentDays = 7 },
                },
            },
        }, options => options.ComparingByValue<LifecycleRuleStatus>());
    }

    [TestMethod]
    public async Task GetBucketLifecycleAsync_ShouldStillDecode_GivenUnknownStatusAndAction()
    {
        this.Respond(200, """{"rules":[{"status":"Paused","filter":{},"expiration":{"days":3}}]}""");
        var rule = (await this.client.GetBucketLifecycleAsync("avatars")).Rules.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            rule.Status.ToString().Should().Be("Paused", "the service can add a status later");
            rule.NoncurrentVersionExpiration.Should().BeNull("the service can add an action later");
        }
    }

    [TestMethod]
    public async Task GetBucketLifecycleAsync_ShouldThrowWithTheServiceCode_GivenNoStoredPolicy()
    {
        this.Respond(400, """{"statusCode":"404","code":"NoSuchLifecycleConfiguration","error":"NoSuchLifecycleConfiguration","message":"The lifecycle configuration does not exist"}""");
        var act = () => this.client.GetBucketLifecycleAsync("avatars");
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Code.Should().Be("NoSuchLifecycleConfiguration", "the call fails when no policy is stored");
            exception.StatusCode.Should().Be(404, "the statusCode the service puts in its body wins over the transport status");
        }
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"rules":[{"filter":{}}]}""")]
    [DataRow("""{"rules":[{"status":"Enabled","noncurrentVersionExpiration":{}}]}""")]
    public async Task GetBucketLifecycleAsync_ShouldThrowStorageException_GivenBodyThatIsNotAPolicy(string body)
    {
        this.Respond(200, body);
        var act = () => this.client.GetBucketLifecycleAsync("avatars");
        await act.Should().ThrowAsync<SupabaseStorageException>();
    }

    [TestMethod]
    public async Task GetBucketLifecycleAsync_ShouldThrowStorageException_GivenEmptySuccess()
    {
        this.Respond(200, "null");
        var act = () => this.client.GetBucketLifecycleAsync("avatars");
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Message
            .Should().Be("The storage service returned an empty lifecycle policy.");
    }

    [TestMethod]
    public async Task GetBucketLifecycleAsync_ShouldCancelTheRequest_GivenCanceledToken()
    {
        this.Respond(200, """{"rules":[]}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.client.GetBucketLifecycleAsync("avatars", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void GetBucketLifecycleAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, """{"rules":[]}""");
        BlockingCaller.Completes(() => this.client.GetBucketLifecycleAsync("avatars"))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
