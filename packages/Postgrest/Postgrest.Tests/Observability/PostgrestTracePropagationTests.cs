using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Core.Http;
using Supabase.Postgrest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Observability;

/// <summary>
/// Every Database request carries the trace context of the current <see cref="Activity"/>, unless
/// <see cref="Supabase.Postgrest.ClientOptions.TracePropagation"/> is disabled (client.observability.trace_propagation).
/// </summary>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class PostgrestTracePropagationTests
{
    [TestMethod]
    public async Task Get_ShouldSendTheTraceContext()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Enabled).Table<Todo>().Get();
        this.sentTraceParents.Should().ContainSingle().Which.Should().Equal([activity.Id], "the request carries the active operation's trace context");
    }

    [TestMethod]
    public async Task Get_ShouldSendNoTraceContext_GivenPropagationDisabled()
    {
        using var activity = Source.StartActivity("operation")!;
        await this.ClientWith(TracePropagation.Disabled).Table<Todo>().Get();
        this.sentTraceParents.Should().ContainSingle().Which.Should().BeEmpty("Disabled means the SDK injects nothing itself");
    }

    private Client ClientWith(TracePropagation propagation) =>
        new(this.server.Url!, new ClientOptions { HttpClient = this.RecordingClient(), TracePropagation = propagation });

    private const string SourceName = "Postgrest.Tests.TracePropagation";

    private static readonly ActivitySource Source = new(SourceName);

    private readonly List<IReadOnlyList<string>> sentTraceParents = new();
    private WireMockServer server = null!;
    private ActivityListener listener = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
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
