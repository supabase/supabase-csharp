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
using Supabase.Storage.ListNamespaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.ListNamespaces;

/// <summary>
/// Contract tests for listing namespaces: the GET request the catalog puts on the wire (method, path, query,
/// headers), how the page is mapped, and how each failure the Iceberg route can give is surfaced.
/// Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ListNamespacesContractTests
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
    public async Task ListNamespacesAsync_ShouldSendBodylessGetWithoutQuery_GivenNoOptions()
    {
        this.Respond(200, "{\"namespaces\":[]}");
        await this.Catalog().ListNamespacesAsync();
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("GET", "the Iceberg route that lists namespaces is GET /:prefix/namespaces");
            request.Path.Should().Be(NamespacesPath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Query.Should().BeNullOrEmpty("an option left unset is not sent");
            request.Body.Should().BeNullOrEmpty();
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldSendOnlyTheOptionsSet()
    {
        this.Respond(200, "{\"namespaces\":[]}");
        await this.Catalog().ListNamespacesAsync(new ListNamespacesOptions { PageSize = 10 });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith($"{NamespacesPath}?pageSize=10");
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldSendParentPageTokenAndPageSize()
    {
        this.Respond(200, "{\"namespaces\":[]}");
        await this.Catalog().ListNamespacesAsync(new ListNamespacesOptions
        {
            Parent = IcebergNamespace.Of("sales", "europe"),
            PageToken = "a/b",
            PageSize = 10,
        });
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWithEquivalentOf($"{NamespacesPath}?parent=sales%1Feurope&pageToken=a%2Fb&pageSize=10",
                "the parent joins its levels with the 0x1F separator, encoded once");
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldMapNamespacesAndNextPageToken()
    {
        this.Respond(200, "{\"namespaces\":[[\"sales\"],[\"ops\"]],\"next-page-token\":\"abc\"}");
        var page = await this.Catalog().ListNamespacesAsync();
        using (new AssertionScope())
        {
            page.Namespaces.Should().Equal(IcebergNamespace.Of("sales"), IcebergNamespace.Of("ops"));
            page.NextPageToken.Should().Be("abc");
        }
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldMapMultiLevelNamespaces()
    {
        this.Respond(200, "{\"namespaces\":[[\"sales\",\"europe\"]]}");
        (await this.Catalog().ListNamespacesAsync()).Namespaces.Should().Equal(IcebergNamespace.Of("sales", "europe"));
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldReturnLastPage_GivenNoNextPageToken()
    {
        this.Respond(200, "{\"namespaces\":[[\"sales\"]]}");
        (await this.Catalog().ListNamespacesAsync()).NextPageToken.Should().BeNull("Storage sends no token, whatever the page size");
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldReturnEmptyPage_GivenNoNamespacesInResponse()
    {
        this.Respond(200, "{}");
        var page = await this.Catalog().ListNamespacesAsync();
        using (new AssertionScope())
        {
            page.Namespaces.Should().BeEmpty();
            page.NextPageToken.Should().BeNull();
        }
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldThrowNotFound_GivenMissingBucket()
    {
        this.Respond(404, Envelope("Catalog name \"analytics-data\" not found", "NoSuchCatalog", 404));
        var act = () => this.Catalog().ListNamespacesAsync();
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be("NoSuchCatalog");
        }
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().ListNamespacesAsync();
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(200, "{}");
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.ListNamespacesAsync();
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task ListNamespacesAsync_ShouldHonorCancellation()
    {
        this.Respond(200, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().ListNamespacesAsync(null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void ListNamespacesAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, "{}");
        BlockingCaller.Completes(() => this.Catalog().ListNamespacesAsync())
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(NamespacesPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
