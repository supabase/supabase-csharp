using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Functions;
using Supabase.Functions.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Functions.Tests;

/// <summary>
/// Contract tests for the diagnostics the client emits through <see cref="System.Diagnostics"/>
/// (the <c>supabase.functions</c> <see cref="ActivitySource"/> and <see cref="Meter"/>): the span's
/// OpenTelemetry tags and error status, the invocation duration histogram and its dimensions, and
/// the sanitization rule that telemetry must never carry the query string, request body, or a token.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ObservabilityContractTests
{
    private const string FunctionName = "hello";
    private const string SecretBodyValue = "secret-body-value-42";

    private readonly List<Activity> activities = new();
    private readonly List<KeyValuePair<double, Dictionary<string, object?>>> measurements = new();
    private ActivityListener activityListener = null!;
    private MeterListener meterListener = null!;
    private Instrument? durationInstrument;
    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.client = new Client($"{this.server.Url}/functions/v1");
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FunctionsDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => this.activities.Add(activity)
        };
        ActivitySource.AddActivityListener(this.activityListener);
        this.meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != FunctionsDiagnostics.SourceName)
                    return;
                this.durationInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }
        };
        this.meterListener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            var tagValues = new Dictionary<string, object?>();
            foreach (var tag in tags)
                tagValues[tag.Key] = tag.Value;
            this.measurements.Add(new KeyValuePair<double, Dictionary<string, object?>>(value, tagValues));
        });
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
    public async Task InvokeSpan_ShouldRecordUrlWithoutQueryString()
    {
        this.MockInvokeOk();
        await this.Invoke();
        this.InvokeSpan().GetTagItem("url.full").Should().Be($"{this.server.Url}/functions/v1/{FunctionName}",
            "the query string must never be recorded");
    }

    [TestMethod]
    public async Task InvokeSpan_ShouldFollowOpenTelemetryConventions()
    {
        this.MockInvokeOk();
        await this.Invoke();
        var span = this.InvokeSpan();
        using (new AssertionScope())
        {
            span.Kind.Should().Be(ActivityKind.Client);
            span.GetTagItem("http.request.method").Should().Be("POST");
            span.GetTagItem("http.response.status_code").Should().Be(200);
            span.GetTagItem("faas.invoked_name").Should().Be(FunctionName);
        }
    }

    [TestMethod]
    public async Task InvokeSpan_ShouldMarkError_GivenFailedStatus()
    {
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));
        var act = this.Invoke;
        await act.Should().ThrowAsync<FunctionsException>();
        var span = this.InvokeSpan();
        using (new AssertionScope())
        {
            span.Status.Should().Be(ActivityStatusCode.Error);
            span.GetTagItem("http.response.status_code").Should().Be(500);
        }
    }

    [TestMethod]
    public async Task InvokeSpan_ShouldMarkError_GivenRelayErrorOnSuccessStatus()
    {
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("x-relay-error", "true").WithBody("relayed"));
        var act = this.Invoke;
        await act.Should().ThrowAsync<FunctionsException>();
        var span = this.InvokeSpan();
        using (new AssertionScope())
        {
            span.Status.Should().Be(ActivityStatusCode.Error);
            span.GetTagItem("error.type").Should().Be("x-relay-error");
        }
    }

    [TestMethod]
    public async Task InvokeDuration_ShouldRecordPositiveDurationPerRequest()
    {
        this.MockInvokeOk();
        await this.Invoke();
        this.meterListener.RecordObservableInstruments();
        var measurement = this.measurements.Should().ContainSingle().Which;
        measurement.Key.Should().BeInRange(0, 60, "the value is an elapsed time in seconds, not raw ticks");
        measurement.Key.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task InvokeDuration_ShouldBeTheStandardHttpClientInstrument()
    {
        this.MockInvokeOk();
        await this.Invoke();
        this.durationInstrument!.Name.Should().Be("http.client.request.duration",
            "every Supabase client records the OpenTelemetry HTTP client metric on its own meter, as Postgrest does");
    }

    [TestMethod]
    public async Task InvokeDuration_ShouldMeasureInSeconds()
    {
        this.MockInvokeOk();
        await this.Invoke();
        this.durationInstrument!.Unit.Should().Be("s", "OpenTelemetry measures HTTP client durations in seconds");
    }

    [TestMethod]
    public async Task InvokeDuration_ShouldBeDescribed()
    {
        this.MockInvokeOk();
        await this.Invoke();
        this.durationInstrument!.Description.Should().Be("Duration of HTTP client requests.",
            "the instrument shares the description of the OpenTelemetry HTTP client metric");
    }

    [TestMethod]
    public async Task InvokeDuration_ShouldCarryTheHttpClientAndFunctionAttributes()
    {
        this.MockInvokeOk();
        await this.Invoke();
        this.measurements.Should().ContainSingle().Which.Value.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["http.request.method"] = "POST",
            ["server.address"] = "localhost",
            ["server.port"] = this.server.Port,
            ["url.scheme"] = "http",
            ["http.response.status_code"] = 200,
            ["faas.invoked_name"] = FunctionName
        }, "the metric carries the OpenTelemetry HTTP client attributes and the invoked function, and no URL path");
    }

    [TestMethod]
    public async Task InvokeDuration_ShouldTagErrorType_GivenFailedStatus()
    {
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));
        var act = this.Invoke;
        await act.Should().ThrowAsync<FunctionsException>();
        this.measurements.Should().ContainSingle().Which.Value["error.type"].Should().Be("500");
    }

    [TestMethod]
    public async Task Telemetry_ShouldNotLeakTheRequestBody()
    {
        this.MockInvokeOk();
        await this.Invoke();
        var recorded = this.activities
            .SelectMany(a => a.TagObjects)
            .Select(tag => tag.Value?.ToString() ?? "")
            .Concat(this.measurements.SelectMany(m => m.Value.Values).Select(v => v?.ToString() ?? ""))
            .Concat(this.activities.Select(a => a.DisplayName));
        recorded.Should().NotContain(value => value.Contains(SecretBodyValue),
            "no span name, tag, or metric dimension may contain the request body");
    }

    private Task<string> Invoke() =>
        this.client.Invoke(FunctionName, options: new Client.InvokeFunctionOptions
        {
            Body = new Dictionary<string, object> { { "name", SecretBodyValue } }
        });

    private void MockInvokeOk() =>
        this.server.Given(Request.Create().WithPath($"/functions/v1/{FunctionName}").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"message\":\"ok\"}"));

    private Activity InvokeSpan() => this.activities.Single(a => a.OperationName == $"POST /functions/v1/{FunctionName}");
}
