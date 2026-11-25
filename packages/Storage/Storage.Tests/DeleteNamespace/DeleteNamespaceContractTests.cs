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

namespace Storage.Tests.DeleteNamespace;

/// <summary>
/// Contract tests for deleting a namespace: the DELETE request the catalog puts on the wire (method, path,
/// namespace encoding, headers, no body) and how each response the Iceberg route can give is surfaced.
/// Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteNamespaceContractTests
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
    public async Task DeleteNamespaceAsync_ShouldSendBodylessDeleteToTheNamespaceRoute()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        await this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("DELETE", "the Iceberg route that drops a namespace is DELETE /:prefix/namespaces/:namespace");
            request.Path.Should().Be($"{NamespacesPath}/sales", "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Body.Should().BeNullOrEmpty("the route discards any request body");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
        }
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldJoinLevelsWithTheUnitSeparator_GivenMultiLevelNamespace()
    {
        this.Respond($"{NamespacesPath}/*", 204, "");
        await this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales", "europe"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith($"{NamespacesPath}/sales%1Feurope", "the Iceberg REST spec joins levels with the 0x1F separator");
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldEscapeEachLevel()
    {
        this.Respond($"{NamespacesPath}/*", 204, "");
        await this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("a/b?c", "d#e"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Url
            .Should().EndWith($"{NamespacesPath}/a%2Fb%3Fc%1Fd%23e", "a level must not start a path segment, query or fragment");
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldComplete_GivenNoContent()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        var act = () => this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
        await act.Should().NotThrowAsync("the route answers 204 with no body");
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldThrowNotFound_GivenMissingNamespace()
    {
        this.Respond($"{NamespacesPath}/sales", 404, Envelope("Namespace not found", "NoSuchNamespaceException", 404));
        var act = () => this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be("NoSuchNamespaceException");
        }
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldThrowNotFound_GivenMissingBucket()
    {
        this.Respond($"{NamespacesPath}/sales", 404, Envelope("Catalog name \"analytics-data\" not found", "NoSuchCatalog", 404));
        var act = () => this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(404);
            exception.Code.Should().Be("NoSuchCatalog");
        }
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldThrow_GivenNamespaceStillHoldsTables()
    {
        this.Respond($"{NamespacesPath}/sales", 400, Envelope("The resource namespace: sales is not empty", "IcebergResourceNotEmpty", 400));
        var act = () => this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(400, "Storage answers 400 where the Iceberg spec says 409");
            exception.Code.Should().Be("IcebergResourceNotEmpty");
        }
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond($"{NamespacesPath}/sales", 403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.DeleteNamespaceAsync(IcebergNamespace.Of("sales"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task DeleteNamespaceAsync_ShouldHonorCancellation()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void DeleteNamespaceAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond($"{NamespacesPath}/sales", 204, "");
        BlockingCaller.Completes(() => this.Catalog().DeleteNamespaceAsync(IcebergNamespace.Of("sales")))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{System.Text.Json.JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(string path, int status, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
