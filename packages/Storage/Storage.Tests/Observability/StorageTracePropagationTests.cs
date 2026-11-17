using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Storage;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using FileOptions = Supabase.Storage.FileOptions;

namespace Storage.Tests.Observability;

/// <summary>
/// Every Storage request (bucket and object calls, direct and resumable uploads, downloads) carries the trace context of the
/// current <see cref="Activity"/>, unless <see cref="ClientOptions.TracePropagation"/> is disabled
/// (client.observability.trace_propagation).
/// </summary>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class StorageTracePropagationTests
{
    private const string Bucket = "bucket";
    private const string SourceName = "Storage.Tests.TracePropagation";
    private const string ResumablePath = "/storage/v1/upload/resumable";

    private static readonly byte[] Payload = [0x1, 0x2, 0x3];
    private static readonly ActivitySource Source = new(SourceName);

    private WireMockServer server = null!;
    private readonly List<IReadOnlyList<string>> sentTraceParents = new();
    private ActivityListener listener = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        UploadMemoryCache.Clear();
        this.server = WireMockServer.Start();
        this.StubStorage();
        this.listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(this.listener);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.listener.Dispose();
        this.server.Stop();
        UploadMemoryCache.Clear();
    }

    [TestMethod]
    public async Task ListBuckets_ShouldSendTheTraceContext()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Enabled).ListBuckets();
        this.TraceParents().Should().Equal([activity.Id], "the request carries the active operation's trace context");
    }

    [TestMethod]
    public async Task Upload_ShouldSendTheTraceContext()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Enabled).From(Bucket).Upload(Payload, "a.bin");
        this.TraceParents().Should().Equal([activity.Id], "uploads carry the active operation's trace context");
    }

    [TestMethod]
    public async Task Download_ShouldSendTheTraceContext()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Enabled).From(Bucket).Download("a.bin", (EventHandler<float>?) null);
        this.TraceParents().Should().Equal([activity.Id], "downloads carry the active operation's trace context");
    }

    [TestMethod]
    public async Task UploadOrResume_ShouldSendTheTraceContextOnEveryRequest()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Enabled).From(Bucket).UploadOrResume(Payload, "a.bin", new FileOptions());
        this.TraceParents().Should().OnlyContain(traceParent => traceParent == activity.Id).And.HaveCount(this.sentTraceParents.Count,
            "each request of a resumable upload carries the operation's trace context");
    }

    [TestMethod]
    public async Task Storage_ShouldSendNoTraceContext_GivenPropagationDisabled()
    {
        using var activity = Source.StartActivity("operation")!;
        var files = this.ClientWith(TracePropagation.Disabled);
        await files.ListBuckets();
        await files.From(Bucket).Upload(Payload, "a.bin");
        await files.From(Bucket).Download("a.bin", (EventHandler<float>?) null);
        await files.From(Bucket).UploadOrResume(Payload, "a.bin", new FileOptions());
        this.TraceParents().Should().BeEmpty("Disabled means the SDK injects nothing itself");
    }

    [TestMethod]
    public async Task Upload_ShouldKeepTheCallersTraceParent_GivenOneInTheFileOptionsHeaders()
    {
        using var activity = Source.StartActivity("operation")!;
        const string callerTraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        await this.ClientWith(TracePropagation.Enabled).From(Bucket).Upload(Payload, "a.bin",
            new FileOptions { Headers = new Dictionary<string, string> { { "traceparent", callerTraceParent } } });
        this.TraceParents().Should().Equal([callerTraceParent], "a header the caller already set wins");
    }

    private Client ClientWith(TracePropagation propagation)
    {
        var http = new HttpClient(new SentHeadersRecorder(this.sentTraceParents, new HttpClientHandler()));
        return new($"{this.server.Url}/storage/v1",
            new ClientOptions { TracePropagation = propagation, HttpRequestClient = http, HttpUploadClient = http, HttpDownloadClient = http },
            new Dictionary<string, string>());
    }

    private IEnumerable<string> TraceParents() => this.sentTraceParents.SelectMany(traceParents => traceParents);

    private sealed class SentHeadersRecorder(List<IReadOnlyList<string>> sink, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            sink.Add(request.Headers.TryGetValues("traceparent", out var values) ? values.ToList() : []);
            return base.SendAsync(request, cancellationToken);
        }
    }

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
