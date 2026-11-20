using System;
using System.Diagnostics;
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
/// The current <see cref="Activity"/> travels with every request the SDK sends as W3C trace context, unless the caller turned
/// propagation off or already set the header (client.observability.trace_propagation).
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
            new RetryOptions(), propagation, null, CancellationToken.None)).Dispose();
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
    public async Task SendAsync_ShouldSendNoTraceContext_GivenNoActivity()
    {
        Activity.Current = null;
        (await SentThrough(TracePropagation.Enabled)).Headers.Contains("traceparent").Should().BeFalse(
            "without an active operation there is no context to propagate");
    }

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
        public HttpRequestMessage? Sent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Sent = request;
            return Task.FromResult(new HttpResponseMessage());
        }
    }
}
