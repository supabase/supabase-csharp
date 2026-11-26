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
using Supabase.Storage.AnalyticsBuckets.Iceberg.UpdateTable;
using Supabase.Storage.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.AnalyticsBuckets.Iceberg.UpdateTable;

/// <summary>
/// Contract tests for updating a table: the POST request the catalog puts on the wire (method, path, table and
/// namespace encoding, headers), how the table metadata after the commit is surfaced, and how each failure the Iceberg
/// route can give is surfaced. Failure bodies are pinned from the storage route source and the Iceberg REST spec, not
/// observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateTableContractTests
{
    private const string TablePath = "/storage/v1/iceberg/v1/analytics-data/namespaces/sales/tables/events";
    private static readonly IcebergTable Events = IcebergNamespace.Of("sales").Table("events");
    private static readonly IcebergTableChanges NewSchema = new()
    {
        Schema = new IcebergSchema { Fields = new[] { new IcebergField { Id = 1, Name = "id", Type = IcebergPrimitiveType.Long, Required = true } } },
    };
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
    public async Task UpdateTableAsync_ShouldPostToTheTableRoute()
    {
        this.Respond(TablePath, 200, Fixture("TableUpdated.json"));
        await this.Catalog().UpdateTableAsync(Events, NewSchema);
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST", "the Iceberg route that commits table changes is POST /:prefix/namespaces/:namespace/tables/:table");
            request.Path.Should().Be(TablePath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
            request.Headers["Content-Type"].Should().ContainSingle().Which.Should().StartWith("application/json");
        }
    }

    [TestMethod]
    public async Task UpdateTableAsync_ShouldEscapeTheTableNameAsOneSegment()
    {
        this.Respond("/storage/v1/iceberg/v1/*", 200, Fixture("TableUpdated.json"));
        await this.Catalog().UpdateTableAsync(IcebergNamespace.Of("sales", "europe").Table("a/b?c#d"), NewSchema);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith("/namespaces/sales%1Feurope/tables/a%2Fb%3Fc%23d", "a name must not start a path segment, query or fragment");
    }

    [TestMethod]
    public async Task UpdateTableAsync_ShouldReturnTheMetadataAfterTheCommit()
    {
        this.Respond(TablePath, 200, Fixture("TableUpdated.json"));
        var metadata = await this.Catalog().UpdateTableAsync(Events, NewSchema);
        using (new AssertionScope())
        {
            metadata.MetadataLocation.Should().Be("s3://analytics-data/sales/events/metadata/00002-7a1f0c8e-2b4d-4e6f-9a1b-3c5d7e9f1a2b.metadata.json");
            metadata.CurrentSchema.SchemaId.Should().Be(1, "the schema the commit added is now current");
            metadata.CurrentSchema.Fields.Should().HaveCount(3);
            metadata.LastColumnId.Should().Be(3);
        }
    }

    [TestMethod]
    public async Task UpdateTableAsync_ShouldThrowConflict_GivenTableChangedSinceBasedOn()
    {
        this.Respond(TablePath, 409, Envelope("Requirement failed: current schema changed: expected id 0 != 1", "CommitFailedException", 409));
        var act = () => this.Catalog().UpdateTableAsync(Events, NewSchema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(409, "the Iceberg REST spec answers 409 when a requirement fails");
            exception.Code.Should().Be("CommitFailedException", "Storage passes the catalog's error type through");
        }
    }

    [TestMethod]
    public async Task UpdateTableAsync_ShouldThrow_GivenInvalidChange()
    {
        this.Respond(TablePath, 400, Envelope("Cannot add field country as an identifier field: not a primitive type", "BadRequestException", 400));
        var act = () => this.Catalog().UpdateTableAsync(Events, NewSchema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(400, "the catalog, not the SDK, validates the changes");
            exception.Code.Should().Be("BadRequestException");
        }
    }

    [TestMethod]
    [DataRow("Table not found", "NoSuchTableException")]
    [DataRow("Namespace not found", "NoSuchNamespaceException")]
    [DataRow("Catalog name \"analytics-data\" not found", "NoSuchCatalog")]
    public async Task UpdateTableAsync_ShouldThrowNotFound_GivenMissingTableNamespaceOrBucket(string message, string code)
    {
        this.Respond(TablePath, 404, Envelope(message, code, 404));
        var act = () => this.Catalog().UpdateTableAsync(Events, NewSchema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be(code);
        }
    }

    [TestMethod]
    public async Task UpdateTableAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(TablePath, 403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().UpdateTableAsync(Events, NewSchema);
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task UpdateTableAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(TablePath, 200, Fixture("TableUpdated.json"));
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.UpdateTableAsync(Events, NewSchema);
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task UpdateTableAsync_ShouldHonorCancellation()
    {
        this.Respond(TablePath, 200, Fixture("TableUpdated.json"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().UpdateTableAsync(Events, NewSchema, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void UpdateTableAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(TablePath, 200, Fixture("TableUpdated.json"));
        BlockingCaller.Completes(() => this.Catalog().UpdateTableAsync(Events, NewSchema))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnalyticsBuckets", "Iceberg", "UpdateTable", "Data", name));

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
