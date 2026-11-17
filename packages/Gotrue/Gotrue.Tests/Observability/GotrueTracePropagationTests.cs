using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Gotrue.Tests.Observability;

/// <summary>
/// Every Auth request carries the trace context of the current <see cref="Activity"/>, unless
/// <see cref="Supabase.Gotrue.ClientOptions.TracePropagation"/> is disabled (client.observability.trace_propagation).
/// </summary>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class GotrueTracePropagationTests
{
    [TestMethod]
    public async Task Settings_ShouldSendTheTraceContext()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Enabled).Settings();
        this.sentTraceParents.Should().ContainSingle().Which.Should().Equal([activity.Id], "the request carries the active operation's trace context");
    }

    [TestMethod]
    public async Task AdminRequest_ShouldSendTheTraceContext()
    {
        using var activity = Source.StartActivity("operation")!;
        await new AdminClient("service-key", this.OptionsWith(TracePropagation.Enabled)).ListUsers();
        this.sentTraceParents.Should().ContainSingle().Which.Should().Equal([activity.Id], "the admin client propagates too");
    }

    [TestMethod]
    public async Task StatelessRequest_ShouldSendTheTraceContext()
    {
        using var activity = Source.StartActivity("operation")!;
        await new StatelessClient().Settings(new StatelessClient.StatelessClientOptions
        {
            Url = this.server.Url!, HttpClient = this.RecordingClient(), TracePropagation = TracePropagation.Enabled
        });
        this.sentTraceParents.Should().ContainSingle().Which.Should().Equal([activity.Id], "the stateless client propagates too");
    }

    [TestMethod]
    public async Task Settings_ShouldSendNoTraceContext_GivenPropagationDisabled()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Disabled).Settings();
        this.sentTraceParents.Should().ContainSingle().Which.Should().BeEmpty("Disabled means the SDK injects nothing itself");
    }

    [TestMethod]
    public async Task StatelessRequest_ShouldSendNoTraceContext_GivenPropagationDisabled()
    {
        using var activity = Source.StartActivity("operation")!;
        await new StatelessClient().Settings(new StatelessClient.StatelessClientOptions
        {
            Url = this.server.Url!, HttpClient = this.RecordingClient(), TracePropagation = TracePropagation.Disabled
        });
        this.sentTraceParents.Should().ContainSingle().Which.Should().BeEmpty("Disabled reaches the stateless client");
    }

    [TestMethod]
    public async Task AdminRequest_ShouldSendNoTraceContext_GivenPropagationDisabled()
    {
        using var activity = Source.StartActivity("operation")!;
        await new AdminClient("service-key", this.OptionsWith(TracePropagation.Disabled)).ListUsers();
        this.sentTraceParents.Should().ContainSingle().Which.Should().BeEmpty("Disabled reaches the admin client");
    }

    private Client ClientWith(TracePropagation propagation) => new(this.OptionsWith(propagation));

    private ClientOptions OptionsWith(TracePropagation propagation) =>
        new() { Url = this.server.Url!, HttpClient = this.RecordingClient(), TracePropagation = propagation };

    private const string SourceName = "Gotrue.Tests.TracePropagation";

    private static readonly ActivitySource Source = new(SourceName);

    private readonly List<IReadOnlyList<string>> sentTraceParents = new();
    private WireMockServer server = null!;
    private ActivityListener listener = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
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
    }

    private HttpClient RecordingClient() => new(new SentHeadersRecorder(this.sentTraceParents, new HttpClientHandler()));

    private sealed class SentHeadersRecorder(List<IReadOnlyList<string>> sink, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            sink.Add(request.Headers.TryGetValues("traceparent", out var values) ? values.ToList() : []);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
