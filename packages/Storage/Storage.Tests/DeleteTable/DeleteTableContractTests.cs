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
using Supabase.Storage.DeleteTable;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.DeleteTable;

/// <summary>
/// Contract tests for deleting a table: the DELETE request the catalog puts on the wire (method, path, table and
/// namespace encoding, purge query, headers, no body) and how each failure the Iceberg route can give is surfaced.
/// Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteTableContractTests
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
    public async Task DeleteTableAsync_ShouldSendBodylessDeleteToTheTableRoute()
    {
        this.Respond(TablePath, 204, "");
        await this.Catalog().DeleteTableAsync(Events);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("DELETE", "the Iceberg route that drops a table is DELETE /:prefix/namespaces/:namespace/tables/:table");
            request.Path.Should().Be(TablePath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Body.Should().BeNullOrEmpty("the route accepts no body");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task DeleteTableAsync_ShouldNotSendPurge_GivenNoneSet()
    {
        this.Respond(TablePath, 204, "");
        await this.Catalog().DeleteTableAsync(Events, new DeleteTableOptions());
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query
            .Should().BeNullOrEmpty("the service owns the default of purgeRequested");
    }

    [TestMethod]
    [DataRow(true, "true")]
    [DataRow(false, "false")]
    public async Task DeleteTableAsync_ShouldSendPurge_GivenSet(bool purge, string expected)
    {
        this.Respond(TablePath, 204, "");
        await this.Catalog().DeleteTableAsync(Events, new DeleteTableOptions { Purge = purge });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Query!["purgeRequested"]
            .Should().Equal(new[] { expected }, "the route reads purgeRequested as the string true or false");
    }

    [TestMethod]
    public async Task DeleteTableAsync_ShouldEscapeTheTableNameAsOneSegment()
    {
        this.Respond("/storage/v1/iceberg/v1/*", 204, "");
        await this.Catalog().DeleteTableAsync(IcebergNamespace.Of("sales", "europe").Table("a/b?c#d"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith("/namespaces/sales%1Feurope/tables/a%2Fb%3Fc%23d", "a name must not start a path segment, query or fragment");
    }

    [TestMethod]
    [DataRow("Table not found", "NoSuchTableException")]
    [DataRow("Namespace not found", "NoSuchNamespaceException")]
    [DataRow("Catalog name \"analytics-data\" not found", "NoSuchCatalog")]
    public async Task DeleteTableAsync_ShouldThrowNotFound_GivenMissingTableNamespaceOrBucket(string message, string code)
    {
        this.Respond(TablePath, 404, Envelope(message, code, 404));
        var act = () => this.Catalog().DeleteTableAsync(Events);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task DeleteTableAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(TablePath, 403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().DeleteTableAsync(Events);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task DeleteTableAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(TablePath, 204, "");
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.DeleteTableAsync(Events);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task DeleteTableAsync_ShouldHonorCancellation()
    {
        this.Respond(TablePath, 204, "");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().DeleteTableAsync(Events, null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void DeleteTableAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(TablePath, 204, "");
        BlockingCaller.Completes(() => this.Catalog().DeleteTableAsync(Events))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
