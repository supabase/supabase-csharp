using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Common;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.NamespaceExists;

/// <summary>
/// Contract tests for checking that a namespace exists: the HEAD request the catalog puts on the wire
/// (method, path, namespace encoding, headers, no body) and how each response the Iceberg route can give
/// is surfaced. Responses are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class NamespaceExistsContractTests
{
    private const string NamespacesPath = "/storage/v1/iceberg/v1/analytics-data/namespaces";
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
    public async Task NamespaceExistsAsync_ShouldSendBodylessHeadToTheNamespaceRoute()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        await this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales"));
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("HEAD", "the Iceberg route that checks a namespace is HEAD /:prefix/namespaces/:namespace");
            request.Path.Should().Be($"{NamespacesPath}/sales", "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Body.Should().BeNullOrEmpty();
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldReturnTrue_GivenNoContent()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        (await this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales")))
            .Should().BeTrue("the route answers 204 when the namespace exists");
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldReturnFalse_GivenNotFound()
    {
        this.Respond($"{NamespacesPath}/sales", 404, "");
        (await this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales")))
            .Should().BeFalse("the route answers 404 when the namespace or its bucket is missing");
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldJoinLevelsWithTheUnitSeparator_GivenMultiLevelNamespace()
    {
        this.Respond($"{NamespacesPath}/*", 204, "");
        await this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales", "europe"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith($"{NamespacesPath}/sales%1Feurope", "the Iceberg REST spec joins levels with the 0x1F separator");
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldEscapeEachLevel()
    {
        this.Respond($"{NamespacesPath}/*", 204, "");
        await this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("a/b?c", "d#e"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith($"{NamespacesPath}/a%2Fb%3Fc%1Fd%23e", "a level must not start a path segment, query or fragment");
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldEscapeTheBucketAsOneSegment()
    {
        this.Respond("/storage/v1/iceberg/v1/*", 204, "");
        await this.client.Analytics.From("a/b?c").NamespaceExistsAsync(IcebergNamespace.Of("sales"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith("/storage/v1/iceberg/v1/a%2Fb%3Fc/namespaces/sales");
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond($"{NamespacesPath}/sales", 403, "{\"error\":{\"message\":\"Access denied: Invalid role\",\"type\":\"AccessDenied\",\"code\":403}}");
        var act = () => this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales"));
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode
            .Should().Be(403, "only the service role may use the catalog");
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldThrow_GivenFeatureNotEnabled()
    {
        this.Respond($"{NamespacesPath}/sales", 409, "{\"error\":{\"message\":\"The feature iceberg_catalog is not enabled for this resource\",\"type\":\"FeatureNotEnabled\",\"code\":409}}");
        var act = () => this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales"));
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode.Should().Be(409);
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldThrow_GivenInternalError()
    {
        this.Respond($"{NamespacesPath}/sales", 500, "{\"error\":{\"message\":\"boom\",\"type\":\"InternalError\",\"code\":500}}");
        var act = () => this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales"));
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason.Should().Be(FailureHint.Reason.Internal);
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.NamespaceExistsAsync(IcebergNamespace.Of("sales"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task NamespaceExistsAsync_ShouldHonorCancellation()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales"), cts.Token);
        await act.Should().ThrowAsync<System.OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void NamespaceExistsAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        BlockingCaller.Completes(() => this.Catalog().NamespaceExistsAsync(IcebergNamespace.Of("sales")))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private Supabase.Storage.Interfaces.IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingHead())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
