#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Observability;

/// <summary>
///     Pins the diagnostics the SDK emits through System.Diagnostics (the "Supabase.Gotrue" ActivitySource
///     and Meter) and the sanitization rule that telemetry and debug output must never contain a token, JWT,
///     query string, or other secret. Exercised against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ObservabilityContractTests
{
    private const string AccessToken = "an-access-token";
    private const string RefreshTokenValue = "a-refresh-token";

    private readonly List<Activity> activities = new();
    private readonly List<RecordedMeasurement> measurements = new();
    private ActivityListener activityListener = null!;
    private Client client = null!;
    private MeterListener meterListener = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = new Client(new ClientOptions
        {
            Url = this.server.Url,
            AutoRefreshToken = true,
            AllowUnconfirmedUserSessions = false,
            Headers = new Dictionary<string, string> { { "apikey", MockGotrueServer.ApiKey } },
        });
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GotrueDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => this.activities.Add(activity),
        };
        ActivitySource.AddActivityListener(this.activityListener);
        this.meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == GotrueDiagnostics.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        this.meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            var tagValues = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagValues[tag.Key] = tag.Value;
            }
            lock (this.measurements)
            {
                this.measurements.Add(new RecordedMeasurement(instrument.Name, instrument.Unit, value, tagValues));
            }
        });
        this.meterListener.Start();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.client.Shutdown();
        this.activityListener.Dispose();
        this.meterListener.Dispose();
        this.server.Dispose();
    }

    [TestMethod]
    public async Task HttpSpan_ShouldRecordUrlWithoutQueryString()
    {
        this.MockTokenSuccess();
        await this.Refresh();
        this.HttpTokenSpan().GetTagItem("url.full").Should().Be($"{this.server.Url}/token",
            "the query string carries grant types and credentials and must never be recorded");
    }

    [TestMethod]
    public async Task HttpSpan_ShouldFollowOpenTelemetryHttpClientConventions()
    {
        this.MockTokenSuccess();
        await this.Refresh();
        var httpSpan = this.HttpTokenSpan();
        httpSpan.Kind.Should().Be(ActivityKind.Client);
        httpSpan.GetTagItem("http.request.method").Should().Be("POST");
        httpSpan.GetTagItem("http.response.status_code").Should().Be(200);
    }

    [TestMethod]
    public async Task DomainSpan_ShouldParentTheHttpSpan()
    {
        this.MockTokenSuccess();
        await this.Refresh();
        var domainSpan = this.activities.Should().ContainSingle(a => a.OperationName == "gotrue.refresh_token").Which;
        this.HttpTokenSpan().ParentSpanId.Should().Be(domainSpan.SpanId);
    }

    [TestMethod]
    public async Task HttpSpan_ShouldBeMarkedError_GivenFailedRequest()
    {
        this.MockTokenFailure();
        await FluentActions.Awaiting(this.Refresh).Should().ThrowAsync<Exception>();
        var httpSpan = this.HttpTokenSpan();
        httpSpan.Status.Should().Be(ActivityStatusCode.Error);
        httpSpan.GetTagItem("http.response.status_code").Should().Be(500);
    }

    [TestMethod]
    public async Task DomainSpan_ShouldBeMarkedError_GivenFailedRefresh()
    {
        this.MockTokenFailure();
        await FluentActions.Awaiting(this.Refresh).Should().ThrowAsync<Exception>();
        var domainSpan = this.activities.Should().ContainSingle(a => a.OperationName == "gotrue.refresh_token").Which;
        domainSpan.Status.Should().Be(ActivityStatusCode.Error,
            "a failed refresh must mark its domain span as error so a silently-swallowed background auto-refresh failure is still visible in traces (issue #91)");
    }

    [TestMethod]
    public async Task RequestDurationHistogram_ShouldRecordOncePerRequest()
    {
        this.MockTokenSuccess();
        await this.Refresh();
        this.meterListener.RecordObservableInstruments();
        var measurement = this.measurements.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            measurement.Instrument.Should().Be("supabase.gotrue.http.request.duration");
            measurement.Unit.Should().Be("s", "the histogram records a duration in seconds");
            measurement.Value.Should().BeGreaterThan(0);
            measurement.Tags.Should().Contain("http.request.method", "POST");
            measurement.Tags.Should().Contain("server.address", new Uri(this.server.Url).Host);
            measurement.Tags.Should().Contain("http.response.status_code", 200);
            measurement.Tags.Should().Contain("url.path", "/token");
            measurement.Tags.Should().NotContainKey("error.type", "a successful request carries no error tag");
        }
    }

    [TestMethod]
    public async Task RequestDurationHistogram_ShouldTagErrorType_GivenFailedRequest()
    {
        this.MockTokenFailure();
        await FluentActions.Awaiting(this.Refresh).Should().ThrowAsync<Exception>();
        this.meterListener.RecordObservableInstruments();
        var measurement = this.measurements.Should().ContainSingle().Which;
        measurement.Tags.Should().Contain("http.response.status_code", 500);
        measurement.Tags.Should().Contain("error.type", "500");
    }

    [TestMethod]
    public async Task Telemetry_ShouldNotContainSessionTokens()
    {
        this.MockTokenSuccess();
        await this.Refresh();
        var tagValues = this.activities
            .SelectMany(a => a.TagObjects)
            .Select(tag => tag.Value?.ToString() ?? "")
            .Concat(this.measurements.SelectMany(m => m.Tags.Values).Select(v => v?.ToString() ?? ""))
            .Concat(this.activities.Select(a => a.DisplayName));
        tagValues.Should().OnlyContain(value =>
            !value.Contains(AccessToken) && !value.Contains(RefreshTokenValue) &&
            !value.Contains("new-access-token") && !value.Contains("new-refresh-token"));
    }

    [TestMethod]
    public async Task DebugLog_ShouldNotContainSessionTokens_GivenFailedRefresh()
    {
        var messages = new List<string>();
#pragma warning disable CS0618 // the obsolete debug surface stays leak-free until it is removed in v8
        this.client.AddDebugListener((message, _) => messages.Add(message));
#pragma warning restore CS0618
        this.MockTokenSuccess();
        await this.Refresh();
        this.server.Reset();
        this.MockTokenFailure();
        var session = await this.client.RetrieveSessionAsync();
        session.Should().NotBeNull("a 5xx is transient, so the session is kept");
        messages.Should().NotBeEmpty("the failed refresh should be reported to debug listeners");
        messages.Should().OnlyContain(message =>
                !message.Contains("new-access-token") && !message.Contains("new-refresh-token"),
            "debug output must never contain the session's access or refresh token");
    }

    private Task Refresh() => this.client.RefreshToken(AccessToken, RefreshTokenValue);

    private Activity HttpTokenSpan() =>
        this.activities.Should().ContainSingle(a => a.OperationName == "POST /token").Which;

    private void MockTokenSuccess() =>
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TokenRefresh", "Fixtures", "token_success.json"))));

    private void MockTokenFailure() =>
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"msg\":\"refresh exploded\"}"));

    private sealed record RecordedMeasurement(string Instrument, string? Unit, double Value, Dictionary<string, object?> Tags);
}
