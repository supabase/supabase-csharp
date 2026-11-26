using System;
using System.Collections.Generic;
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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.TableExists;

/// <summary>
/// Contract tests for checking that a table exists: the HEAD request the catalog puts on the wire (method, path, table
/// and namespace encoding, headers, no body) and how each response the Iceberg route can give is surfaced.
/// Responses are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class TableExistsContractTests
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
    public async Task TableExistsAsync_ShouldSendBodylessHeadToTheTableRoute()
    {
        this.Respond(TablePath, 204, "");
        await this.Catalog().TableExistsAsync(Events);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("HEAD", "the Iceberg route that checks a table is HEAD /:prefix/namespaces/:namespace/tables/:table");
            request.Path.Should().Be(TablePath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Body.Should().BeNullOrEmpty();
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task TableExistsAsync_ShouldReturnTrue_GivenNoContent()
    {
        this.Respond(TablePath, 204, "");
        (await this.Catalog().TableExistsAsync(Events))
            .Should().BeTrue("the route answers 204 when the table exists");
    }

    [TestMethod]
    public async Task TableExistsAsync_ShouldReturnFalse_GivenNotFound()
    {
        this.Respond(TablePath, 404, "");
        (await this.Catalog().TableExistsAsync(Events))
            .Should().BeFalse("the route answers 404 when the table, its namespace or its bucket is missing");
    }

    [TestMethod]
    public async Task TableExistsAsync_ShouldEscapeTheTableNameAsOneSegment()
    {
        this.Respond("/storage/v1/iceberg/v1/*", 204, "");
        await this.Catalog().TableExistsAsync(IcebergNamespace.Of("sales", "europe").Table("a/b?c#d"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith("/namespaces/sales%1Feurope/tables/a%2Fb%3Fc%23d", "a name must not start a path segment, query or fragment");
    }

    [TestMethod]
    public async Task TableExistsAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(TablePath, 403, "{\"error\":{\"message\":\"Access denied: Invalid role\",\"type\":\"AccessDenied\",\"code\":403}}");
        var act = () => this.Catalog().TableExistsAsync(Events);
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.StatusCode
            .Should().Be(403, "only the service role may use the catalog");
    }

    [TestMethod]
    public async Task TableExistsAsync_ShouldThrow_GivenInternalError()
    {
        this.Respond(TablePath, 500, "{\"error\":{\"message\":\"Table shard key not found\",\"type\":\"ShardNotFound\",\"code\":500}}");
        var act = () => this.Catalog().TableExistsAsync(Events);
        (await act.Should().ThrowAsync<SupabaseStorageException>()).Which.Reason.Should().Be(FailureHint.Reason.Internal);
    }

    [TestMethod]
    public async Task TableExistsAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(TablePath, 204, "");
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.TableExistsAsync(Events);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task TableExistsAsync_ShouldHonorCancellation()
    {
        this.Respond(TablePath, 204, "");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().TableExistsAsync(Events, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void TableExistsAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(TablePath, 204, "");
        BlockingCaller.Completes(() => this.Catalog().TableExistsAsync(Events))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingHead())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
