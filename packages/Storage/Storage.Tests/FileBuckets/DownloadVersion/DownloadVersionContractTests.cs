using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.FileBuckets.DownloadVersion;

/// <summary>
/// Contract tests for <c>DownloadVersionAsync</c>: the request it sends to
/// <c>GET /object/{bucket}/{path}</c> and how the route's answers reach the caller.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DownloadVersionContractTests
{
    private const string Path = "/storage/v1/object/avatars/folder/avatar.png";
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
    public async Task DownloadVersionAsync_ShouldGetTheVersionFromTheObjectRoute()
    {
        var payload = Encoding.UTF8.GetBytes("old-bytes");
        this.server.Given(Request.Create().WithPath(Path).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload));
        var bytes = await this.client.From("avatars").DownloadVersionAsync("folder/avatar.png", "version-1");
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            bytes.Should().Equal(payload);
            request.Query!["versionId"].Should().Equal(new[] { "version-1" });
        }
    }

    [TestMethod]
    public async Task DownloadVersionAsync_ShouldThrowNotFound_GivenUnknownVersion()
    {
        this.server.Given(Request.Create().WithPath(Path).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(400).WithHeader("Content-Type", "application/json")
                .WithBody("""{"statusCode":"404","error":"not_found","message":"Object not found","code":"NoSuchKey"}"""));
        var act = () => this.client.From("avatars").DownloadVersionAsync("folder/avatar.png", "unknown");
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(404, "the statusCode the service puts in its body wins over the transport status");
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
        }
    }
}
