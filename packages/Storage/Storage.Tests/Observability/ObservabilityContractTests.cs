using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Storage.Tests.Observability;

/// <summary>
/// Contract tests for the diagnostics the SDK emits through System.Diagnostics
/// (ActivitySource/Meter "supabase.storage") and for the sanitization rule: telemetry must never
/// contain a query string, a signed-URL token, or file contents. Also covers the transfer-size
/// metric that distinguishes uploads/downloads from control-plane requests.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ObservabilityContractTests
{
    private const string Bucket = "bucket";
    private const string SecretToken = "secret-signed-url-token-42";

    private readonly List<Activity> activities = new();
    private readonly List<(string Name, double Value, Dictionary<string, object?> Tags)> measurements = new();
    private ActivityListener activityListener = null!;
    private MeterListener meterListener = null!;
    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/storage/v1", new Dictionary<string, string>
        {
            { "Authorization", "Bearer test-key" }
        });
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == StorageDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => this.activities.Add(activity)
        };
        ActivitySource.AddActivityListener(this.activityListener);
        this.meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == StorageDiagnostics.SourceName)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        this.meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            this.Capture(instrument.Name, value, tags));
        this.meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            this.Capture(instrument.Name, value, tags));
        this.meterListener.Start();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.activityListener.Dispose();
        this.meterListener.Dispose();
        this.server.Stop();
    }

    [TestMethod]
    public async Task ControlPlaneRequest_ShouldEmitAnHttpSpanWithoutTransferDirection()
    {
        this.RespondJson($"/storage/v1/object/list/{Bucket}", "POST", 200, "[]");
        await this.client.From(Bucket).List();
        var span = this.SingleSpan($"POST /storage/v1/object/list/{Bucket}");
        using (new AssertionScope())
        {
            span.GetTagItem("http.request.method").Should().Be("POST");
            span.GetTagItem("http.response.status_code").Should().Be(200);
            span.GetTagItem("storage.transfer.direction").Should().BeNull();
        }
    }

    [TestMethod]
    public async Task ControlPlaneRequest_ShouldRecordTheStandardHttpClientInstrument()
    {
        this.RespondJson($"/storage/v1/object/list/{Bucket}", "POST", 200, "[]");
        await this.client.From(Bucket).List();
        this.measurements.Should().ContainSingle().Which.Name.Should().Be("http.client.request.duration",
            "every Supabase client records the OpenTelemetry HTTP client metric on its own meter, as Postgrest does");
    }

    [TestMethod]
    public async Task ControlPlaneRequest_ShouldCarryTheHttpClientAttributes()
    {
        this.RespondJson($"/storage/v1/object/list/{Bucket}", "POST", 200, "[]");
        await this.client.From(Bucket).List();
        this.measurements.Should().ContainSingle().Which.Tags.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["http.request.method"] = "POST",
            ["server.address"] = "localhost",
            ["server.port"] = this.server.Port,
            ["url.scheme"] = "http",
            ["http.response.status_code"] = 200
        }, "the metric carries the OpenTelemetry HTTP client attributes and no URL path, which holds bucket and object names");
    }

    [TestMethod]
    public async Task Download_ShouldTagTheSpanAndRecordTheTransferSize()
    {
        var payload = Encoding.UTF8.GetBytes("hello-world-download-payload");
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/file.txt").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload));
        var bytes = await this.client.From(Bucket).Download("file.txt", (EventHandler<float>?) null);
        var size = this.measurements.Single(m => m.Name == "supabase.storage.transfer.size");
        using (new AssertionScope())
        {
            bytes.Length.Should().Be(payload.Length);
            this.SingleSpan($"GET /storage/v1/object/{Bucket}/file.txt")
                .GetTagItem("storage.transfer.direction").Should().Be("download");
            size.Value.Should().Be(payload.Length);
            size.Tags["storage.transfer.direction"].Should().Be("download");
            this.measurements.Should().Contain(m => m.Name == "supabase.storage.transfer.duration");
        }
    }

    [TestMethod]
    public async Task Download_ShouldCarryTheTransferAttributes()
    {
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/{Bucket}/file.txt").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("payload"));
        await this.client.From(Bucket).Download("file.txt", (EventHandler<float>?) null);
        this.measurements.Single(m => m.Name == "supabase.storage.transfer.duration").Tags.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["http.request.method"] = "GET",
            ["server.address"] = "localhost",
            ["url.path"] = $"/storage/v1/object/{Bucket}/file.txt",
            ["storage.transfer.direction"] = "download",
            ["http.response.status_code"] = 200
        }, "a transfer is a whole upload or download rather than one HTTP request, so it keeps its own metrics and attributes");
    }

    [TestMethod]
    public async Task Upload_ShouldTagTheSpanAndRecordTheTransferSize()
    {
        var payload = Encoding.UTF8.GetBytes("some-bytes-to-upload");
        this.RespondJson($"/storage/v1/object/{Bucket}/file.bin", "POST", 200, "{\"Key\":\"x\"}");
        await this.client.From(Bucket).Upload(payload, "file.bin");
        var size = this.measurements.Single(m => m.Name == "supabase.storage.transfer.size");
        using (new AssertionScope())
        {
            this.SingleSpan($"POST /storage/v1/object/{Bucket}/file.bin")
                .GetTagItem("storage.transfer.direction").Should().Be("upload");
            size.Value.Should().Be(payload.Length);
            size.Tags["storage.transfer.direction"].Should().Be("upload");
        }
    }

    [TestMethod]
    public async Task FailedRequest_ShouldMarkTheSpanAsError()
    {
        this.server.Given(Request.Create().WithPath($"/storage/v1/object/list/{Bucket}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("{\"message\":\"boom\"}"));
        var act = () => this.client.From(Bucket).List();
        await act.Should().ThrowAsync<Supabase.Storage.Exceptions.SupabaseStorageException>();
        var span = this.SingleSpan($"POST /storage/v1/object/list/{Bucket}");
        using (new AssertionScope())
        {
            span.Status.Should().Be(ActivityStatusCode.Error);
            span.GetTagItem("http.response.status_code").Should().Be(500);
        }
    }

    [TestMethod]
    public async Task Telemetry_ShouldNotLeakTheSignedUrlToken()
    {
        var signedUrl = new UploadSignedUrl(
            new Uri($"{this.server.Url}/storage/v1/object/upload/sign/{Bucket}/file.bin?token={SecretToken}"),
            SecretToken,
            "file.bin");
        this.RespondJson($"/storage/v1/object/upload/sign/{Bucket}/file.bin", "POST", 200, "{\"Key\":\"x\"}");
        await this.client.From(Bucket).UploadToSignedUrl(Encoding.UTF8.GetBytes("data"), signedUrl);
        var recorded = this.activities
            .SelectMany(a => a.TagObjects)
            .Select(tag => tag.Value?.ToString() ?? "")
            .Concat(this.measurements.SelectMany(m => m.Tags.Values).Select(v => v?.ToString() ?? ""));
        using (new AssertionScope())
        {
            this.SingleSpan($"POST /storage/v1/object/upload/sign/{Bucket}/file.bin")
                .GetTagItem("url.full").Should().Be($"{this.server.Url}/storage/v1/object/upload/sign/{Bucket}/file.bin",
                    "the signed-URL token lives in the query string and must never be recorded");
            recorded.Should().NotContain(value => value.Contains(SecretToken),
                "no span name, tag, or metric dimension may contain the signed-URL token");
        }
    }

    private void Capture(string name, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagValues = new Dictionary<string, object?>();
        foreach (var tag in tags)
            tagValues[tag.Key] = tag.Value;
        this.measurements.Add((name, value, tagValues));
    }

    private void RespondJson(string path, string method, int statusCode, string body) =>
        this.server.Given(Request.Create().WithPath(path).UsingMethod(method))
            .RespondWith(Response.Create().WithStatusCode(statusCode)
                .WithHeader("Content-Type", "application/json").WithBody(body));

    private Activity SingleSpan(string operationName) =>
        this.activities.Single(a => a.OperationName == operationName);
}
