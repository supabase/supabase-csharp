using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Observability;

/// <summary>
///     The diagnostics the SDK emits through <c>System.Diagnostics</c> (the "supabase.postgrest"
///     ActivitySource and Meter): an HTTP client span and a duration histogram, tagged per OpenTelemetry HTTP
///     conventions — and the sanitization rule that telemetry must never carry a query string or a filter value.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class TelemetryTests
{
    private const string SecretFilterValue = "secret-filter-value-42";

    private readonly List<Activity> activities = new();
    private readonly List<KeyValuePair<double, Dictionary<string, object?>>> measurements = new();
    private readonly List<Instrument> instruments = new();
    private ActivityListener activityListener = null!;
    private MeterListener meterListener = null!;
    private WireMockServer server = null!;
    private Client client = null!;

    [Table("todos")]
    private class Todo : BaseModel
    {
        [PrimaryKey("id", false)] public int Id { get; set; }
        [Column("name")] public string? Name { get; set; }
    }

    [TestInitialize]
    public void SetUp()
    {
        this.server = WireMockServer.Start();
        this.client = new Client(this.server.Url!, new ClientOptions());
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PostgrestDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => this.activities.Add(activity)
        };
        ActivitySource.AddActivityListener(this.activityListener);
        this.meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != PostgrestDiagnostics.SourceName)
                    return;
                this.instruments.Add(instrument);
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
    public void TearDown()
    {
        this.activityListener.Dispose();
        this.meterListener.Dispose();
        this.server.Stop();
    }

    [TestMethod]
    public async Task HttpSpan_ShouldRedactFilterValuesInTheUrlAndKeepTheirKeys()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        this.SingleHttpSpan().GetTagItem("url.full").Should().Be($"{this.server.Url}/todos?name=REDACTED",
            "the query keys are kept but the filter value must never be recorded");
    }

    [TestMethod]
    public async Task HttpSpan_ShouldKeepTheValuesOfTheKeysThatOnlyShapeTheResult()
    {
        this.MockTodosOk();
        await this.client.Table<Todo>().Select("id,name").Filter("name", Operator.Equals, SecretFilterValue)
            .Order("id", Ordering.Descending).Limit(10).Offset(5).Get();
        var url = (string) this.SingleHttpSpan().GetTagItem("url.full")!;
        var query = url.Substring(url.IndexOf('?') + 1).Split('&').Select(Uri.UnescapeDataString);
        query.Should().BeEquivalentTo("select=id,name", "name=REDACTED", "order=id.desc.nullsfirst", "limit=10", "offset=5");
    }

    [TestMethod]
    public async Task HttpSpan_ShouldRedactEveryOccurrenceOfARepeatedKey()
    {
        this.MockTodosOk();
        await this.client.Table<Todo>().Filter("id", Operator.GreaterThan, 18).Filter("id", Operator.LessThan, 65).Get();
        this.SingleHttpSpan().GetTagItem("url.full").Should().Be($"{this.server.Url}/todos?id=REDACTED&id=REDACTED");
    }

    [TestMethod]
    public async Task HttpSpan_ShouldFollowOpenTelemetryConventions()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        var span = this.SingleHttpSpan();
        span.Kind.Should().Be(ActivityKind.Client);
        span.GetTagItem("http.request.method").Should().Be("GET");
        span.GetTagItem("http.response.status_code").Should().Be(200);
    }

    [TestMethod]
    public async Task HttpSpan_ShouldTagTheOperationAndTable_GivenATableCall()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        var span = this.SingleHttpSpan();
        span.GetTagItem("supabase.postgrest.operation").Should().Be("select");
        span.GetTagItem("supabase.postgrest.table").Should().Be("todos");
        span.GetTagItem("supabase.postgrest.function").Should().BeNull();
    }

    [TestMethod]
    public async Task HttpSpan_ShouldTagTheOperationAndFunction_GivenAnRpcCall()
    {
        this.MockRpcOk();
        await this.client.Rpc("get_scores", null);
        var span = this.activities.Single(a => a.OperationName == "POST /rpc/get_scores");
        span.GetTagItem("supabase.postgrest.operation").Should().Be("rpc");
        span.GetTagItem("supabase.postgrest.function").Should().Be("get_scores");
        span.GetTagItem("supabase.postgrest.table").Should().BeNull();
    }

    [TestMethod]
    public async Task HttpSpan_ShouldTagACountAsASelect()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingHead())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Range", "0-0/1"));
        await this.client.Table<Todo>().Count(CountType.Exact);
        this.activities.Single(a => a.OperationName == "HEAD /todos")
            .GetTagItem("supabase.postgrest.operation").Should().Be("select");
    }

    [TestMethod]
    public async Task HttpSpan_ShouldBeMarkedAnError_GivenAFailedRequest()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("{\"message\":\"boom\"}"));
        await ((Func<Task>) this.FilteredGet).Should().ThrowAsync<PostgrestException>();
        var span = this.SingleHttpSpan();
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("http.response.status_code").Should().Be(500);
    }

    [TestMethod]
    public async Task DurationHistogram_ShouldBeTheStandardHttpClientInstrument()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        this.instruments.Should().ContainSingle().Which.Name.Should().Be("http.client.request.duration");
    }

    [TestMethod]
    public async Task DurationHistogram_ShouldMeasureInSeconds()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        this.instruments.Single().Unit.Should().Be("s");
    }

    [TestMethod]
    public async Task DurationHistogram_ShouldBeDescribed()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        this.instruments.Single().Description.Should().Be("Duration of HTTP client requests.");
    }

    [TestMethod]
    public async Task DurationHistogram_ShouldRecordTheElapsedSeconds()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        this.measurements.Single().Key.Should().BeInRange(0.000001, 30);
    }

    [TestMethod]
    public async Task DurationHistogram_ShouldCarryTheHttpAndTableAttributes_GivenATableCall()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        this.measurements.Single().Value.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["http.request.method"] = "GET",
            ["server.address"] = "localhost",
            ["server.port"] = this.server.Port,
            ["url.scheme"] = "http",
            ["http.response.status_code"] = 200,
            ["supabase.postgrest.operation"] = "select",
            ["supabase.postgrest.table"] = "todos"
        });
    }

    [TestMethod]
    public async Task DurationHistogram_ShouldCarryTheFunctionAttribute_GivenAnRpcCall()
    {
        this.MockRpcOk();
        await this.client.Rpc("get_scores", null);
        this.measurements.Single().Value.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["http.request.method"] = "POST",
            ["server.address"] = "localhost",
            ["server.port"] = this.server.Port,
            ["url.scheme"] = "http",
            ["http.response.status_code"] = 200,
            ["supabase.postgrest.operation"] = "rpc",
            ["supabase.postgrest.function"] = "get_scores"
        });
    }

    [TestMethod]
    public async Task DurationHistogram_ShouldCarryTheErrorType_GivenAFailedRequest()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("{\"message\":\"boom\"}"));
        await ((Func<Task>) this.FilteredGet).Should().ThrowAsync<PostgrestException>();
        this.measurements.Single().Value.Should().Contain(new KeyValuePair<string, object?>("error.type", "500"));
    }

    [TestMethod]
    [DataRow("GET", false, false, false, "select")]
    [DataRow("HEAD", false, false, false, "select")]
    [DataRow("POST", true, false, false, "insert")]
    [DataRow("PATCH", false, true, false, "update")]
    [DataRow("POST", true, false, true, "upsert")]
    [DataRow("DELETE", false, false, false, "delete")]
    public void ResolveOperation_ShouldNameTheLogicalOperation(string method, bool isInsert, bool isUpdate, bool isUpsert, string expected) =>
        PostgrestInstrumentation.ResolveOperation(new System.Net.Http.HttpMethod(method), isInsert, isUpdate, isUpsert).Should().Be(expected);

    [TestMethod]
    public async Task DurationHistogram_ShouldCarryNoUrl()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        this.measurements.Single().Value.Keys.Should().NotContain(key => key.StartsWith("url.") && key != "url.scheme");
    }

    [TestMethod]
    public async Task Telemetry_ShouldNeverContainAFilterValue()
    {
        this.MockTodosOk();
        await this.FilteredGet();
        var recorded = this.activities
            .SelectMany(a => a.TagObjects)
            .Select(tag => tag.Value?.ToString() ?? "")
            .Concat(this.measurements.SelectMany(m => m.Value.Values).Select(v => v?.ToString() ?? ""))
            .Concat(this.activities.Select(a => a.DisplayName));
        recorded.Should().NotContain(value => value.Contains(SecretFilterValue),
            "no span name, tag, or metric dimension may contain a column filter value");
    }

    private Task<Supabase.Postgrest.Responses.ModeledResponse<Todo>> FilteredGet() =>
        this.client.Table<Todo>().Filter("name", Operator.Equals, SecretFilterValue).Get();

    private void MockTodosOk() =>
        this.server.Given(Request.Create().WithPath("/todos").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("[]"));

    private void MockRpcOk() =>
        this.server.Given(Request.Create().WithPath("/rpc/get_scores").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));

    private Activity SingleHttpSpan() =>
        this.activities.Single(a => a.OperationName == "GET /todos");
}
