using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.AnalyticsBuckets.Iceberg;
using Supabase.Storage.AnalyticsBuckets.Iceberg.Common;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.LoadNamespace;

/// <summary>
/// Contract tests for loading a namespace: the GET request the catalog puts on the wire (method, path,
/// namespace encoding, headers, no body) and how each response the Iceberg route can give is surfaced.
/// Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class LoadNamespaceContractTests
{
    private const string NamespacesPath = "/storage/v1/iceberg/v1/analytics-data/namespaces";
    private const string SalesBody = "{\"namespace\":[\"sales\"],\"properties\":{\"owner\":\"data-team\",\"env\":\"prod\"}}";
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
    public async Task LoadNamespaceAsync_ShouldSendBodylessGetToTheNamespaceRoute()
    {
        this.Respond($"{NamespacesPath}/sales", 200, SalesBody);
        await this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"));
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("GET", "the Iceberg route that loads a namespace is GET /:prefix/namespaces/:namespace");
            request.Path.Should().Be($"{NamespacesPath}/sales", "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Body.Should().BeNullOrEmpty("a GET carries no payload");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldJoinLevelsWithTheUnitSeparator_GivenMultiLevelNamespace()
    {
        this.Respond($"{NamespacesPath}/*", 200, SalesBody);
        await this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales", "europe"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith($"{NamespacesPath}/sales%1Feurope", "the Iceberg REST spec joins levels with the 0x1F separator");
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldEscapeEachLevel()
    {
        this.Respond($"{NamespacesPath}/*", 200, SalesBody);
        await this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("a/b?c", "d#e"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith($"{NamespacesPath}/a%2Fb%3Fc%1Fd%23e", "a level must not start a path segment, query or fragment");
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldReturnTheStoredNamespace()
    {
        this.Respond($"{NamespacesPath}/sales", 200, SalesBody);
        (await this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"))).Namespace
            .Should().Be(IcebergNamespace.Of("sales"));
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldReturnTheStoredProperties()
    {
        this.Respond($"{NamespacesPath}/sales", 200, SalesBody);
        (await this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"))).Properties
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["owner"] = "data-team", ["env"] = "prod" });
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldReturnNoProperties_GivenResponseOmitsThem()
    {
        this.Respond($"{NamespacesPath}/sales", 200, "{\"namespace\":[\"sales\"]}");
        (await this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"))).Properties.Should().BeEmpty();
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldReturnTheRequestedNamespace_GivenResponseOmitsIt()
    {
        this.Respond($"{NamespacesPath}/sales", 200, "{\"properties\":{}}");
        (await this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"))).Namespace
            .Should().Be(IcebergNamespace.Of("sales"), "the caller asked for it, so an identifier-less body still names it");
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldThrowNotFound_GivenMissingNamespace()
    {
        this.Respond($"{NamespacesPath}/sales", 404, Envelope("Namespace not found", "NoSuchNamespaceException", 404));
        var act = () => this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be("NoSuchNamespaceException");
        }
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldThrowNotFound_GivenMissingBucket()
    {
        this.Respond($"{NamespacesPath}/sales", 404, Envelope("Catalog name \"analytics-data\" not found", "NoSuchCatalog", 404));
        var act = () => this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(404);
            exception.Code.Should().Be("NoSuchCatalog");
        }
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond($"{NamespacesPath}/sales", 403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond($"{NamespacesPath}/sales", 200, SalesBody);
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.LoadNamespaceAsync(IcebergNamespace.Of("sales"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task LoadNamespaceAsync_ShouldHonorCancellation()
    {
        this.Respond($"{NamespacesPath}/sales", 200, SalesBody);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales"), cts.Token);
        await act.Should().ThrowAsync<System.OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void LoadNamespaceAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond($"{NamespacesPath}/sales", 200, SalesBody);
        BlockingCaller.Completes(() => this.Catalog().LoadNamespaceAsync(IcebergNamespace.Of("sales")))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{System.Text.Json.JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
