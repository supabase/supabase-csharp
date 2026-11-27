using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core;
using Supabase.Storage;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using FileOptions = Supabase.Storage.FileOptions;

namespace Storage.Tests.Observability;

/// <summary>
/// Every Storage request (bucket and object calls, direct and resumable uploads, downloads) identifies the SDK in a
/// single <c>X-Client-Info</c> header, or carries the value the application supplied in any casing
/// (client.observability.client_info).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StorageClientInfoTests
{
    private const string Bucket = "bucket";
    private const string ResumablePath = "/storage/v1/upload/resumable";
    private const string SuppliedValue = "my-lib/1.0";

    private static readonly byte[] Payload = [0x1, 0x2, 0x3];
    private static readonly string SdkValue = Util.GetAssemblyVersion(typeof(Client));

    private WireMockServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        UploadMemoryCache.Clear();
        this.server = WireMockServer.Start();
        this.StubStorage();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.server.Stop();
        UploadMemoryCache.Clear();
    }

    [TestMethod]
    public async Task ListBuckets_ShouldSendTheSdkValue()
    {
        await this.Client().ListBuckets();
        this.SentClientInfo().Should().Equal(SdkValue);
    }

    [TestMethod]
    public async Task Upload_ShouldSendTheSdkValue()
    {
        await this.Client().From(Bucket).Upload(Payload, "a.bin");
        this.SentClientInfo().Should().Equal(SdkValue);
    }

    [TestMethod]
    public async Task Download_ShouldSendTheSdkValue()
    {
        await this.Client().From(Bucket).Download("a.bin", (EventHandler<float>?) null);
        this.SentClientInfo().Should().Equal(SdkValue);
    }

    [TestMethod]
    public async Task UploadOrResume_ShouldSendTheSdkValueOnEveryRequest()
    {
        await this.Client().From(Bucket).UploadOrResume(Payload, "a.bin", new FileOptions());
        this.SentClientInfo().Should().HaveCount(this.server.LogEntries.Count()).And.OnlyContain(value => value == SdkValue,
            "each request of a resumable upload identifies the SDK");
    }

    [TestMethod]
    public async Task Storage_ShouldSendTheSuppliedValue_GivenClientHeaderInAnyCasing()
    {
        var files = this.Client(new Dictionary<string, string> { ["x-client-info"] = SuppliedValue });
        await files.ListBuckets();
        await files.From(Bucket).Upload(Payload, "a.bin");
        await files.From(Bucket).Download("a.bin", (EventHandler<float>?) null);
        await files.From(Bucket).UploadOrResume(Payload, "a.bin", new FileOptions());
        this.SentClientInfo().Should().HaveCount(this.server.LogEntries.Count()).And.OnlyContain(value => value == SuppliedValue,
            "an application-supplied value replaces the SDK's on every request");
    }

    [TestMethod]
    public async Task ListBuckets_ShouldSendTheSuppliedValue_GivenDynamicHeaders()
    {
        var client = this.Client();
        client.GetHeaders = () => new Dictionary<string, string> { ["X-Client-Info"] = SuppliedValue };
        await client.ListBuckets();
        this.SentClientInfo().Should().Equal(SuppliedValue);
    }

    private Client Client(Dictionary<string, string>? headers = null) =>
        new($"{this.server.Url}/storage/v1", new ClientOptions(), headers ?? new Dictionary<string, string>());

    private IEnumerable<string> SentClientInfo() =>
        this.server.LogEntries.SelectMany(entry => entry.RequestMessage!.Headers!.TryGetValue("X-Client-Info", out var values) ? values : []);

    private void StubStorage()
    {
        var length = Payload.Length.ToString(CultureInfo.InvariantCulture);
        this.server.Given(Request.Create().UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/a.bin").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{\"Key\":\"x\"}"));
        this.server.Given(Request.Create().WithPath(ResumablePath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(201)
                .WithHeader("Location", $"{this.server.Url}{ResumablePath}/new-session")
                .WithHeader("Tus-Resumable", "1.0.0")
                .WithHeader("Upload-Offset", "0"));
        this.server.Given(Request.Create().UsingHead())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Tus-Resumable", "1.0.0")
                .WithHeader("Upload-Offset", "0")
                .WithHeader("Upload-Length", length));
        this.server.Given(Request.Create().UsingPatch())
            .RespondWith(Response.Create().WithStatusCode(204)
                .WithHeader("Tus-Resumable", "1.0.0")
                .WithHeader("Upload-Offset", length));
    }
}
