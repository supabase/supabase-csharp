using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Storage.Tests.Support;
using Supabase.Storage;
using Supabase.Storage.Common;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;
using Supabase.Storage.ListTables;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.ListTables;

/// <summary>
/// Contract tests for listing the tables of a namespace: the GET request the catalog puts on the wire (method, path,
/// namespace encoding, paging query, headers, no body), how the page in the response is mapped, and how each failure
/// the Iceberg route can give is surfaced. Failure bodies are pinned from the storage route source, not observed on a
/// running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListTablesContractTests
{
    private const string TablesPath = "/storage/v1/iceberg/v1/analytics-data/namespaces/sales/tables";
    private const string Listed = "{\"identifiers\":[{\"namespace\":[\"sales\"],\"name\":\"events\"},{\"namespace\":[\"sales\"],\"name\":\"orders\"}]}";
    private static readonly IcebergNamespace Sales = IcebergNamespace.Of("sales");
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
    public async Task ListTablesAsync_ShouldSendBodylessGetToTheTablesRouteOfTheNamespace()
    {
        this.Respond(TablesPath, 200, Listed);
        await this.Catalog().ListTablesAsync(Sales);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("GET", "the Iceberg route that lists tables is GET /:prefix/namespaces/:namespace/tables");
            request.Path.Should().Be(TablesPath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Body.Should().BeNullOrEmpty();
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldSendNoPagingParameter_GivenNoOptions()
    {
        this.Respond(TablesPath, 200, Listed);
        await this.Catalog().ListTablesAsync(Sales);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query
            .Should().BeNullOrEmpty("an option left unset is not sent");
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldSendPageSizeAndToken_GivenOptions()
    {
        this.Respond(TablesPath, 200, Listed);
        await this.Catalog().ListTablesAsync(Sales, new ListTablesOptions { PageSize = 2, PageToken = "next/page token" });
        var query = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query!;
        using (new AssertionScope())
        {
            query["pageSize"].Should().Equal(new[] { "2" }, "the Iceberg REST spec names it pageSize");
            query["pageToken"].Should().Equal(new[] { "next/page token" }, "the Iceberg REST spec names it pageToken");
        }
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldJoinNamespaceLevelsWithTheUnitSeparator()
    {
        this.Respond("/storage/v1/iceberg/v1/*", 200, Listed);
        await this.Catalog().ListTablesAsync(IcebergNamespace.Of("sales", "europe"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().Contain("/namespaces/sales%1Feurope/tables", "the Iceberg REST spec joins levels with the 0x1F separator");
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldReturnTheListedTablesInOrder()
    {
        this.Respond(TablesPath, 200, Listed);
        (await this.Catalog().ListTablesAsync(Sales)).Tables
            .Should().Equal(Sales.Table("events"), Sales.Table("orders"));
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldReturnTheNamespaceTheServiceSent()
    {
        this.Respond(TablesPath, 200, "{\"identifiers\":[{\"namespace\":[\"sales\",\"europe\"],\"name\":\"events\"}]}");
        (await this.Catalog().ListTablesAsync(Sales)).Tables
            .Should().ContainSingle().Which.Should().Be(IcebergNamespace.Of("sales", "europe").Table("events"));
    }

    [TestMethod]
    [DataRow("{\"identifiers\":[{\"name\":\"events\"}]}")]
    [DataRow("{\"identifiers\":[{\"namespace\":[],\"name\":\"events\"}]}")]
    public async Task ListTablesAsync_ShouldReturnTheListedNamespace_GivenIdentifierWithoutNamespace(string body)
    {
        this.Respond(TablesPath, 200, body);
        (await this.Catalog().ListTablesAsync(Sales)).Tables
            .Should().ContainSingle().Which.Should().Be(Sales.Table("events"));
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldReturnNoTable_GivenResponseOmitsIdentifiers()
    {
        this.Respond(TablesPath, 200, "{}");
        (await this.Catalog().ListTablesAsync(Sales)).Tables.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldReturnTheNextPageToken()
    {
        this.Respond(TablesPath, 200, "{\"identifiers\":[],\"next-page-token\":\"page-2\"}");
        (await this.Catalog().ListTablesAsync(Sales)).NextPageToken.Should().Be("page-2");
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldReturnNoNextPageToken_GivenLastPage()
    {
        this.Respond(TablesPath, 200, Listed);
        (await this.Catalog().ListTablesAsync(Sales)).NextPageToken.Should().BeNull("Storage never sends one today");
    }

    [TestMethod]
    [DataRow("Namespace not found", "NoSuchNamespaceException")]
    [DataRow("Catalog name \"analytics-data\" not found", "NoSuchCatalog")]
    public async Task ListTablesAsync_ShouldThrowNotFound_GivenMissingNamespaceOrBucket(string message, string code)
    {
        this.Respond(TablesPath, 404, Envelope(message, code, 404));
        var act = () => this.Catalog().ListTablesAsync(Sales);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldThrow_GivenNegativePageSize()
    {
        this.Respond(TablesPath, 400, Envelope("querystring/pageSize must be >= 0", "InvalidRequest", 400));
        var act = () => this.Catalog().ListTablesAsync(Sales, new ListTablesOptions { PageSize = -1 });
        (await act.Should().ThrowAsync<SupabaseStorageException>("the service, not the SDK, validates the page size"))
            .Which.StatusCode.Should().Be(400);
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(TablesPath, 403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().ListTablesAsync(Sales);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(TablesPath, 200, Listed);
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.ListTablesAsync(Sales);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task ListTablesAsync_ShouldHonorCancellation()
    {
        this.Respond(TablesPath, 200, Listed);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().ListTablesAsync(Sales, null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void ListTablesAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(TablesPath, 200, Listed);
        BlockingCaller.Completes(() => this.Catalog().ListTablesAsync(Sales))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
