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
using Supabase.Storage.CreateNamespace;
using Supabase.Storage.Exceptions;
using Supabase.Storage.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.CreateNamespace;

/// <summary>
/// Contract tests for creating a namespace: the POST request the catalog puts on the wire (method, path, body,
/// headers), how the response is mapped, and how each failure the Iceberg route can give is surfaced.
/// Failure bodies are pinned from the storage route source, not observed on a running service.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class CreateNamespaceContractTests
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
    public async Task CreateNamespaceAsync_ShouldPostToTheNamespacesRoute()
    {
        this.Respond(200, "{\"namespace\":[\"sales\"],\"properties\":{}}");
        await this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        var request = this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
        using (new AssertionScope())
        {
            request.Method.Should().Be("POST", "the Iceberg route that creates a namespace is POST /:prefix/namespaces");
            request.Path.Should().Be(NamespacesPath, "the bucket is the Iceberg warehouse, sent as the path prefix");
            request.Headers!["Authorization"].Should().ContainSingle().Which.Should().Be("Bearer service-key");
            request.Headers["Content-Type"].Should().ContainSingle().Which.Should().StartWith("application/json");
        }
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldSendLevelsAndProperties()
    {
        this.Respond(200, "{\"namespace\":[\"sales\"],\"properties\":{}}");
        await this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"), new CreateNamespaceOptions { Properties = new Dictionary<string, string> { ["owner"] = "data-team" } });
        using var body = JsonDocument.Parse(this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body!);
        using (new AssertionScope())
        {
            body.RootElement.GetProperty("namespace").EnumerateArray().Should().ContainSingle().Which.GetString().Should().Be("sales");
            body.RootElement.GetProperty("properties").GetProperty("owner").GetString().Should().Be("data-team");
        }
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldOmitProperties_GivenNoneSet()
    {
        this.Respond(200, "{\"namespace\":[\"sales\"]}");
        await this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"), new CreateNamespaceOptions());
        using var body = JsonDocument.Parse(this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Body!);
        body.RootElement.TryGetProperty("properties", out _).Should().BeFalse("an empty properties object adds nothing for the service to store");
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldReturnTheStoredNamespaceAndProperties()
    {
        this.Respond(200, "{\"namespace\":[\"sales\"],\"properties\":{\"owner\":\"data-team\",\"env\":\"prod\"}}");
        var created = await this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        using (new AssertionScope())
        {
            created.Namespace.Should().Be(IcebergNamespace.Of("sales"));
            created.Properties.Should().BeEquivalentTo(new Dictionary<string, string> { ["owner"] = "data-team", ["env"] = "prod" });
        }
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldReturnMergedProperties_GivenNamespaceAlreadyExists()
    {
        this.Respond(200, "{\"namespace\":[\"sales\"],\"properties\":{\"stable\":\"kept\",\"overwrite\":\"new\"}}");
        var created = await this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        created.Properties.Should().HaveCount(2, "Storage upserts and answers 200 with the merged properties where the Iceberg spec answers 409");
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldReturnNoProperties_GivenResponseOmitsThem()
    {
        this.Respond(200, "{\"namespace\":[\"sales\"]}");
        (await this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"))).Properties.Should().BeEmpty();
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldReturnTheRequestedNamespace_GivenResponseOmitsIt()
    {
        this.Respond(200, "{}");
        (await this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"))).Namespace.Should().Be(IcebergNamespace.Of("sales"));
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldThrowInvalidInput_GivenInvalidName()
    {
        this.Respond(400, Envelope("Resource name must not start with the reserved prefix \"aws\"", "InvalidParameter", 400));
        var act = () => this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("awsnamespace"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(400);
            exception.Code.Should().Be("InvalidParameter");
        }
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldThrowNotFound_GivenMissingBucket()
    {
        this.Respond(404, Envelope("Catalog name \"analytics-data\" not found", "NoSuchCatalog", 404));
        var act = () => this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(FailureHint.Reason.NotFound);
            exception.Code.Should().Be("NoSuchCatalog");
        }
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldThrow_GivenNamespaceLimitReached()
    {
        this.Respond(409, Envelope("The maximum number of this resource (namespace) 25 is reached", "IcebergMaximumResourceLimit", 409));
        var act = () => this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(409);
            exception.Code.Should().Be("IcebergMaximumResourceLimit");
        }
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldThrow_GivenForbidden()
    {
        this.Respond(403, Envelope("Access denied: Invalid role", "AccessDenied", 403));
        var act = () => this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        var exception = (await act.Should().ThrowAsync<SupabaseStorageException>()).Which;
        using (new AssertionScope())
        {
            exception.StatusCode.Should().Be(403, "only the service role may use the catalog");
            exception.Code.Should().Be("AccessDenied");
        }
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldReadHeadersAtCallTime()
    {
        this.Respond(200, "{}");
        var catalog = this.Catalog();
        this.client.GetHeaders = () => new Dictionary<string, string> { { "apikey", "rotated" } };
        await catalog.CreateNamespaceAsync(IcebergNamespace.Of("sales"));
        this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["apikey"]
            .Should().ContainSingle().Which.Should().Be("rotated");
    }

    [TestMethod]
    public async Task CreateNamespaceAsync_ShouldHonorCancellation()
    {
        this.Respond(200, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales"), null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token must reach the HTTP call");
    }

    [TestMethod]
    public void CreateNamespaceAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.Respond(200, "{}");
        BlockingCaller.Completes(() => this.Catalog().CreateNamespaceAsync(IcebergNamespace.Of("sales")))
            .Should().BeTrue("the SDK never resumes on the caller's synchronization context");
    }

    private static string Envelope(string message, string type, int code) =>
        $"{{\"error\":{{\"message\":{JsonSerializer.Serialize(message)},\"type\":\"{type}\",\"code\":{code}}}}}";

    private IIcebergCatalog Catalog() => this.client.Analytics.From("analytics-data");

    private void Respond(int status, string body) =>
        this.server.Given(Request.Create().WithPath(NamespacesPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
