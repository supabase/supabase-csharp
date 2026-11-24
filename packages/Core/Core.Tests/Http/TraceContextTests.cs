using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Core.Http;

namespace Core.Tests.Http;

/// <summary>
/// The current <see cref="Activity"/> travels with every request the SDK sends as W3C trace context, and without one the SDK
/// originates an unsampled trace, unless the caller turned propagation off or already set the header (client.observability.trace_propagation).
/// </summary>
[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public class TraceContextTests
{
    private const string SourceName = "Core.Tests.TraceContext";

    private static readonly ActivitySource Source = new(SourceName);

    private ActivityListener listener = null!;

    [TestInitialize]
    public void StartListening()
    {
        this.listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(this.listener);
    }

    [TestCleanup]
    public void StopListening() => this.listener.Dispose();

    private static async Task<HttpRequestMessage> SentThrough(TracePropagation propagation, Func<HttpRequestMessage>? request = null)
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        (await RetryExecutor.SendAsync(client, request ?? (() => new HttpRequestMessage(HttpMethod.Get, "http://localhost/")),
            new RetryOptions(), propagation, null, TimeProvider.System, CancellationToken.None)).Dispose();
        return handler.Sent!;
    }

    [TestMethod]
    public async Task SendAsync_ShouldSendTheCurrentTraceContext_GivenPropagationEnabled()
    {
        using var activity = Source.StartActivity("operation")!;
        var sent = await SentThrough(TracePropagation.Enabled);
        sent.Headers.GetValues("traceparent").Should().ContainSingle().Which.Should().Be(activity.Id,
            "the request carries the W3C trace context of the active operation");
    }

    [TestMethod]
    public async Task SendAsync_ShouldSendTraceStateAndBaggage_GivenTheActivityCarriesThem()
    {
        using var activity = Source.StartActivity("operation")!;
        activity.TraceStateString = "vendor=value";
        activity.AddBaggage("tenant", "acme");
        var sent = await SentThrough(TracePropagation.Enabled);
        using (new AssertionScope())
        {
            sent.Headers.GetValues("tracestate").Should().ContainSingle().Which.Should().Be("vendor=value");
            sent.Headers.GetValues("baggage").Should().ContainSingle().Which.Should().Be("tenant = acme");
        }
    }

    [TestMethod]
    public async Task SendAsync_ShouldSendNoTraceContext_GivenPropagationDisabled()
    {
        using var activity = Source.StartActivity("operation")!;
        (await SentThrough(TracePropagation.Disabled)).Headers.Contains("traceparent").Should().BeFalse(
            "Disabled means the SDK injects nothing itself");
    }

    [TestMethod]
    public async Task SendAsync_ShouldOriginateAnUnsampledTraceParent_GivenNoActivity()
    {
        Activity.Current = null;
        var sent = await SentThrough(TracePropagation.Enabled);
        sent.Headers.GetValues("traceparent").Should().ContainSingle().Which.Should().MatchRegex("^00-[0-9a-f]{32}-[0-9a-f]{16}-00$",
            "without an active operation the SDK starts a trace that nothing downstream records");
    }

    [TestMethod]
    public async Task SendAsync_ShouldOriginateANewTracePerCall_GivenNoActivity()
    {
        Activity.Current = null;
        var first = (await SentThrough(TracePropagation.Enabled)).Headers.GetValues("traceparent").Single();
        var second = (await SentThrough(TracePropagation.Enabled)).Headers.GetValues("traceparent").Single();
        TraceIdOf(first).Should().NotBe(TraceIdOf(second), "each call is its own trace");
    }

    [TestMethod]
    public async Task SendAsync_ShouldShareTheTraceAcrossRetries_GivenNoActivity()
    {
        Activity.Current = null;
        var handler = new CapturingHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var options = new RetryOptions { MaxRetries = 1, BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero };
        (await RetryExecutor.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, "http://localhost/"),
            options, TracePropagation.Enabled, null, TimeProvider.System, CancellationToken.None)).Dispose();

        var attempts = handler.Received.Select(request => request.Headers.GetValues("traceparent").Single()).ToList();
        using (new AssertionScope())
        {
            attempts.Should().HaveCount(2);
            TraceIdOf(attempts[0]).Should().Be(TraceIdOf(attempts[1]), "the attempts belong to one call");
            SpanIdOf(attempts[0]).Should().NotBe(SpanIdOf(attempts[1]), "each attempt is its own request");
        }
    }

    [TestMethod]
    public async Task SendAsync_ShouldKeepTheCallersTraceParentUnchanged_GivenNoActivity()
    {
        const string callerTraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        Activity.Current = null;
        var sent = await SentThrough(TracePropagation.Enabled, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
            request.Headers.Add("traceparent", callerTraceParent);
            return request;
        });
        sent.Headers.GetValues("traceparent").Should().ContainSingle().Which.Should().Be(callerTraceParent,
            "an existing trace is forwarded as is, flags included");
    }

    [TestMethod]
    public async Task SendAsync_ShouldKeepTheActivitysFlags_GivenASampledActivity()
    {
        using var activity = Source.StartActivity("operation")!;
        var sent = await SentThrough(TracePropagation.Enabled);
        sent.Headers.GetValues("traceparent").Single().Should().EndWith("-01", "a trace that already exists keeps its sampling decision");
    }

    private static string TraceIdOf(string traceParent) => traceParent.Split('-')[1];

    private static string SpanIdOf(string traceParent) => traceParent.Split('-')[2];

    [TestMethod]
    public async Task SendAsync_ShouldKeepTheCallersTraceParent_GivenTheRequestAlreadyHasOne()
    {
        const string callerTraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        using var activity = Source.StartActivity("operation")!;
        var sent = await SentThrough(TracePropagation.Enabled, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
            request.Headers.Add("traceparent", callerTraceParent);
            return request;
        });
        sent.Headers.GetValues("traceparent").Should().ContainSingle().Which.Should().Be(callerTraceParent,
            "a header the caller already set wins");
    }

    [TestMethod]
    public async Task SendAsync_ShouldStillSendTheRequest_GivenThePropagatorThrows()
    {
        var original = DistributedContextPropagator.Current;
        var propagator = Substitute.For<DistributedContextPropagator>();
        propagator.When(substitute => substitute.Inject(Arg.Any<Activity>(), Arg.Any<object>(), Arg.Any<DistributedContextPropagator.PropagatorSetterCallback>()))
            .Do(_ => throw new InvalidOperationException("broken propagator"));
        DistributedContextPropagator.Current = propagator;
        try
        {
            using var activity = Source.StartActivity("operation")!;
            (await SentThrough(TracePropagation.Enabled)).Should().NotBeNull("a failing propagator never fails the request");
        }
        finally
        {
            DistributedContextPropagator.Current = original;
        }
    }

    [TestMethod]
    public async Task PublicSendAsync_ShouldSendTheCurrentTraceContext()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        using var activity = Source.StartActivity("operation")!;
        (await RetryExecutor.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, "http://localhost/"), new RetryOptions())).Dispose();
        handler.Sent!.Headers.Contains("traceparent").Should().BeTrue("tracing is on by default for every caller of the executor");
    }

    [TestMethod]
    public async Task PublicSendAsyncWithCompletionOption_ShouldSendTheCurrentTraceContext()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        using var activity = Source.StartActivity("operation")!;
        (await RetryExecutor.SendAsync(client, () => new HttpRequestMessage(HttpMethod.Get, "http://localhost/"), new RetryOptions(),
            HttpCompletionOption.ResponseHeadersRead, CancellationToken.None)).Dispose();
        handler.Sent!.Headers.Contains("traceparent").Should().BeTrue();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> statuses;

        public CapturingHandler(params HttpStatusCode[] statuses) => this.statuses = new Queue<HttpStatusCode>(statuses);

        public HttpRequestMessage? Sent => this.Received.LastOrDefault();

        public List<HttpRequestMessage> Received { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Received.Add(request);
            return Task.FromResult(new HttpResponseMessage(this.statuses.Count > 0 ? this.statuses.Dequeue() : HttpStatusCode.OK));
        }
    }
}
