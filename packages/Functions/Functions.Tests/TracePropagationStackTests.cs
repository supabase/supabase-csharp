using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Functions.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Functions;

namespace Functions.Tests;

/// <summary>
/// The trace context of the current <see cref="Activity"/> reaches a function running on the local Supabase stack, which
/// reports the <c>traceparent</c> it received (client.observability.trace_propagation).
/// </summary>
[TestClass]
[TestCategory("E2E")]
[DoNotParallelize]
public class TracePropagationStackTests
{
    private const string SourceName = "Functions.Tests.TracePropagationStack";

    private static readonly ActivitySource Source = new(SourceName);

    [TestMethod]
    public async Task Invoke_ShouldDeliverTheTraceIdToTheFunction()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = Source.StartActivity("operation")!;
        var echoed = await new Client("http://localhost:54321/functions/v1").Invoke("echo-headers", LocalStackToken.Issue());
        JsonDocument.Parse(echoed).RootElement.GetProperty("traceparent").GetString().Should().Contain(activity.TraceId.ToString(),
            "the server side of the request belongs to the client-side trace");
    }
}
