using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

namespace Storage.Tests.AnalyticsBuckets.Iceberg.LoadTable;

/// <summary>
/// Contract tests for loading a table: the GET request the catalog puts on the wire (method, path, table and namespace
/// encoding, headers, no body), how the table metadata in the response is surfaced, and how each failure the Iceberg
/// route can give is surfaced. Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class LoadTableContractTests
{
    private const string TablePath = "/storage/v1/iceberg/v1/analytics-data/namespaces/sales/tables/events";
    private static readonly IcebergTable Events = IcebergNamespace.Of("sales").Table("events");
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
    public async Task LoadTableAsync_ShouldSendBodylessGetToTheTableRoute()
    {
        this.Respond(TablePath, 200, Fixture("TableLoaded.json"));
        await this.Catalog().LoadTableAsync(Events);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("GET", "the Iceberg route that loads a table is GET /:prefix/namespaces/:namespace/tables/:table");
            request.Path.Should().Be(TablePath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Body.Should().BeNullOrEmpty();
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task LoadTableAsync_ShouldSendNoQuery()
    {
        this.Respond(TablePath, 200, Fixture("TableLoaded.json"));
        await this.Catalog().LoadTableAsync(Events);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query
            .Should().BeNullOrEmpty("Storage decides which snapshots to return and ignores the snapshots query");
    }

    [TestMethod]
    public async Task LoadTableAsync_ShouldEscapeTheTableNameAsOneSegment()
    {
        this.Respond("/storage/v1/iceberg/v1/*", 200, Fixture("TableLoaded.json"));
        await this.Catalog().LoadTableAsync(IcebergNamespace.Of("sales", "europe").Table("a/b?c#d"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith("/namespaces/sales%1Feurope/tables/a%2Fb%3Fc%23d", "a name must not start a path segment, query or fragment");
    }

    [TestMethod]
    public async Task LoadTableAsync_ShouldReturnTheTableMetadata()
    {
        this.Respond(TablePath, 200, Fixture("TableLoaded.json"));
        var metadata = await this.Catalog().LoadTableAsync(Events);
        using (new AssertionScope())
        {
            metadata.MetadataLocation.Should().Be("s3://analytics-data/sales/events/metadata/00001-5d3c6a1e-4f51-4a9e-9a43-0d6c2f7f3b21.metadata.json");
            metadata.TableUuid.Should().Be(Guid.Parse("b734865e-f9a5-4654-8b0b-e15683fcb195"));
            metadata.CurrentSchema.Fields.Should().HaveCount(2);
            metadata.DefaultPartitionSpec.Fields.Should().ContainSingle().Which.Transform.Should().Be(IcebergTransform.Day);
            metadata.LastColumnId.Should().Be(2, "a new column takes a higher identifier");
            metadata.Properties.Should().Equal(new Dictionary<string, string> { ["write.parquet.compression-codec"] = "zstd" });
        }
    }

    [TestMethod]
    [DataRow("Table not found", "NoSuchTableException")]
    [DataRow("Namespace not found", "NoSuchNamespaceException")]
    [DataRow("Catalog name \"analytics-data\" not found", "NoSuchCatalog")]
    public async Task LoadTableAsync_ShouldThrowNotFound_GivenMissingTableNamespaceOrBucket(string message, string code)
    {
        this.Respond(TablePath, 404, Envelope(message, code, 404));
        var act = () => this.Catalog().LoadTableAsync(Events);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task LoadTableAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(TablePath, 403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().LoadTableAsync(Events);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task LoadTableAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(TablePath, 200, Fixture("TableLoaded.json"));
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.LoadTableAsync(Events);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task LoadTableAsync_ShouldHonorCancellation()
    {
        this.Respond(TablePath, 200, Fixture("TableLoaded.json"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().LoadTableAsync(Events, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void LoadTableAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(TablePath, 200, Fixture("TableLoaded.json"));
        BlockingCaller.Completes(() => this.Catalog().LoadTableAsync(Events))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnalyticsBuckets", "Iceberg", "LoadTable", "Data", name));

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
