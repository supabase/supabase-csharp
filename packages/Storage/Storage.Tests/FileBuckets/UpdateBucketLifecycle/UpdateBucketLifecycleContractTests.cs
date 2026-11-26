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

namespace Storage.Tests.FileBuckets.UpdateBucketLifecycle;

/// <summary>
/// Contract tests for <c>UpdateBucketLifecycleAsync</c>: the request it sends to
/// <c>PUT /bucket/{id}/lifecycle</c> and how the route's answers reach the caller.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateBucketLifecycleContractTests
{
    private const string Path = "/storage/v1/bucket/avatars/lifecycle";
    private const string StoredPolicy = """{"rules":[{"id":"rule-9f2c","status":"Enabled","filter":{},"noncurrentVersionExpiration":{"noncurrentDays":30}}]}""";

    private static readonly BucketLifecycle Policy = new()
    {
        Rules = new[]
        {
            new LifecycleRule { Status = LifecycleRuleStatus.Enabled, NoncurrentVersionExpiration = new NoncurrentVersionExpiration { NoncurrentDays = 30 } },
        },
    };

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
    public async Task UpdateBucketLifecycleAsync_ShouldPutThePolicyToTheLifecycleRoute()
    {
        this.Respond(200, StoredPolicy);
        await this.client.UpdateBucketLifecycleAsync("avatars", Policy);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("PUT");
            request.Path.Should().Be(Path);
            request.Body.Should().Be("""{"rules":[{"status":"Enabled","filter":{},"noncurrentVersionExpiration":{"noncurrentDays":30}}]}""",
                "a rule without an id leaves it to the service and carries the empty filter the route requires");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-role-key");
        }
    }

    [TestMethod]
    public async Task UpdateBucketLifecycleAsync_ShouldReturnTheStoredPolicy()
    {
        this.Respond(200, StoredPolicy);
        (await this.client.UpdateBucketLifecycleAsync("avatars", Policy)).Rules.Should().ContainSingle()
            .Which.Id.Should().Be("rule-9f2c", "the response is the stored policy, including the generated rule ids");
    }

    [TestMethod]
    public async Task UpdateBucketLifecycleAsync_ShouldOmitTheExpiration_GivenRuleWithoutOne()
    {
        this.Respond(200, StoredPolicy);
        await this.client.UpdateBucketLifecycleAsync("avatars", new BucketLifecycle
        {
            Rules = new[] { new LifecycleRule { Id = "bare", Status = LifecycleRuleStatus.Enabled } },
        });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body
            .Should().Be("""{"rules":[{"id":"bare","status":"Enabled","filter":{}}]}""", "the service judges a rule without an action, not the SDK");
    }

    [TestMethod]
    public async Task UpdateBucketLifecycleAsync_ShouldThrowWithTheServiceCode_GivenRejectedRules()
    {
        this.Respond(400, """{"statusCode":"400","code":"InvalidParameter","error":"InvalidParameter","message":"Rule 1 noncurrentDays must be at least 1"}""");
        var act = () => this.client.UpdateBucketLifecycleAsync("avatars", Policy);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>("the service validates the rules, not the SDK")).Which;
        using (new AssertionScope())
        {
            exception.Code.Should().Be("InvalidParameter", "the caller branches on the code the service sends");
            exception.Message.Should().Contain("noncurrentDays must be at least 1", "the service explains which rule it rejected");
        }
    }

    [TestMethod]
    public async Task UpdateBucketLifecycleAsync_ShouldThrowStorageException_GivenEmptySuccess()
    {
        this.Respond(200, "null");
        var act = () => this.client.UpdateBucketLifecycleAsync("avatars", Policy);
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Message
            .Should().Be("The storage service returned an empty lifecycle policy.");
    }

    [TestMethod]
    public async Task UpdateBucketLifecycleAsync_ShouldCancelTheRequest_GivenCanceledToken()
    {
        this.Respond(200, StoredPolicy);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.client.UpdateBucketLifecycleAsync("avatars", Policy, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void UpdateBucketLifecycleAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, StoredPolicy);
        BlockingCaller.Completes(() => this.client.UpdateBucketLifecycleAsync("avatars", Policy))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(Path).UsingPut())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
