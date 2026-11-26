using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Diagnostics;

namespace Core.Tests.Diagnostics;

/// <summary>
/// Covers <see cref="HttpClientRequestDuration"/>: the OpenTelemetry <c>http.client.request.duration</c> instrument
/// every Supabase client records on its own meter, and the HTTP client attributes it carries.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class HttpClientRequestDurationTests
{
    private const int NotFound = 404;
    private const double SecondsWithinATest = 1;
    private static readonly Uri RequestUri = new("https://project.supabase.co:8443/rest/v1/todos?select=id#fragment");

    private readonly List<Instrument> instruments = new();
    private readonly List<double> durations = new();
    private Meter meter = null!;
    private MeterListener listener = null!;

    [TestInitialize]
    public void SetUp()
    {
        this.meter = new Meter($"supabase.test.{Guid.NewGuid():N}");
        this.listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter != this.meter)
                    return;
                this.instruments.Add(instrument);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        this.listener.SetMeasurementEventCallback<double>((_, value, _, _) => this.durations.Add(value));
        this.listener.Start();
    }

    [TestCleanup]
    public void TearDown()
    {
        this.listener.Dispose();
        this.meter.Dispose();
    }

    [TestMethod]
    public void On_ShouldNameTheStandardHttpClientInstrument()
    {
        HttpClientRequestDuration.On(this.meter);
        this.instruments.Should().ContainSingle().Which.Name.Should().Be("http.client.request.duration",
            "the OpenTelemetry HTTP client semantic conventions name the request duration metric");
    }

    [TestMethod]
    public void On_ShouldMeasureInSeconds()
    {
        HttpClientRequestDuration.On(this.meter);
        this.instruments.Should().ContainSingle().Which.Unit.Should().Be("s",
            "the OpenTelemetry HTTP client semantic conventions measure durations in seconds");
    }

    [TestMethod]
    public void On_ShouldDescribeTheInstrument()
    {
        HttpClientRequestDuration.On(this.meter);
        this.instruments.Should().ContainSingle().Which.Description.Should().Be("Duration of HTTP client requests.",
            "every client shares one description for the shared instrument");
    }

    [TestMethod]
    public void Enabled_ShouldBeTrue_GivenAListener() =>
        HttpClientRequestDuration.On(this.meter).Enabled.Should().BeTrue("the listener enabled the instrument");

    [TestMethod]
    public void Enabled_ShouldBeFalse_GivenNoListener()
    {
        using var unobservedMeter = new Meter($"supabase.unobserved.{Guid.NewGuid():N}");
        HttpClientRequestDuration.On(unobservedMeter).Enabled.Should().BeFalse("nothing listens to this meter");
    }

    [TestMethod]
    public void Record_ShouldRecordTheElapsedSeconds()
    {
        HttpClientRequestDuration.On(this.meter).Record(Stopwatch.GetTimestamp(), new TagList());
        this.durations.Should().ContainSingle().Which.Should().BeInRange(0, SecondsWithinATest,
            "the value is the time since the start timestamp, in seconds rather than ticks");
    }

    [TestMethod]
    public void TagsOf_ShouldCarryTheHttpClientAttributes_GivenAResponse() =>
        ToDictionary(HttpClientRequestDuration.TagsOf(HttpMethod.Get, RequestUri, NotFound, "404")).Should().BeEquivalentTo(
            new Dictionary<string, object?>
            {
                ["http.request.method"] = "GET",
                ["server.address"] = "project.supabase.co",
                ["server.port"] = 8443,
                ["url.scheme"] = "https",
                ["http.response.status_code"] = NotFound,
                ["error.type"] = "404"
            }, "the OpenTelemetry HTTP client attributes describe the request without its path, query, or fragment");

    [TestMethod]
    public void TagsOf_ShouldOmitTheStatusAndErrorType_GivenASuccessWithoutResponse() =>
        ToDictionary(HttpClientRequestDuration.TagsOf(HttpMethod.Post, RequestUri, null, null)).Keys.Should().BeEquivalentTo(
            new[] { "http.request.method", "server.address", "server.port", "url.scheme" },
            "a status code and an error type are recorded only when the request has them");

    private static Dictionary<string, object?> ToDictionary(TagList tags)
    {
        var values = new Dictionary<string, object?>();
        foreach (var tag in tags)
            values[tag.Key] = tag.Value;
        return values;
    }
}
