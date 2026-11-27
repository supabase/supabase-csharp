using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Postgrest.Tests.Support;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Explain;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Explain;

/// <summary>
///     <c>ExplainAsync</c> reads with the <c>application/vnd.pgrst.plan+json</c> Accept media type, names the
///     requested <c>EXPLAIN</c> details in its <c>options</c> parameter, returns the plan as PostgREST sends it,
///     and maps PostgREST's <c>PGRST107</c> refusal to <see cref="FailureHint.Reason.MediaTypeNotAcceptable" />.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ExplainRequestTests
{
    private const string PlanBody = """[{"Plan":{"Node Type":"Seq Scan","Total Cost":22.0}}]""";

    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void SetUp()
    {
        this.server = WireMockServer.Start();
        this.client = new Client(this.server.Url!, new ClientOptions());
    }

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task ExplainAsync_ShouldReturnThePlanAsSent()
    {
        this.RespondWith(200, PlanBody);
        (await this.client.Table<Todo>().ExplainAsync()).GetRawText()
            .Should().Be(PlanBody, "explain returns the EXPLAIN (FORMAT JSON) output PostgREST produced");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldSendGetWithThePlanAcceptHeader()
    {
        this.RespondWith(200, PlanBody);
        await this.client.Table<Todo>().ExplainAsync();
        var request = this.server.LogEntries.Single().RequestMessage!;
        (request.Method, request.Headers!["Accept"].Single())
            .Should().Be(("GET", "application/vnd.pgrst.plan+json"),
                "PostgREST plans the JSON representation by default, so no options and no for parameter are sent");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldNameEveryRequestedDetail_GivenEveryOption()
    {
        this.RespondWith(200, PlanBody);
        await this.client.Table<Todo>().ExplainAsync(new ExplainOptions { Analyze = true, Verbose = true, Settings = true, Buffers = true, Wal = true });
        this.server.LogEntries.Single().RequestMessage!.Headers!["Accept"].Single()
            .Should().Be("application/vnd.pgrst.plan+json; options=analyze|verbose|settings|buffers|wal",
                "PostgREST reads the EXPLAIN options as a pipe-separated list");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldThrowMediaTypeNotAcceptable_GivenPlansAreDisabled()
    {
        this.RespondWith(406, """{"code":"PGRST107","details":null,"hint":null,"message":"None of these media types are available: application/vnd.pgrst.plan+json"}""");
        var act = () => this.client.Table<Todo>().ExplainAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.MediaTypeNotAcceptable, "PostgREST refuses plan requests with PGRST107 while db-plan-enabled is off");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldNotReportMediaTypeNotAcceptable_GivenAnotherNotAcceptableAnswer()
    {
        this.RespondWith(406, """{"code":"PGRST106","details":null,"hint":null,"message":"Invalid schema: private"}""");
        var act = () => this.client.Table<Todo>().ExplainAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.Unknown, "only PGRST107 says that the requested media type is not available");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldNotCarryTheQueryIntoTheNextRequest()
    {
        this.RespondWith(200, PlanBody);
        var table = this.client.Table<Todo>();
        await table.Filter("id", Operator.Equals, "1").ExplainAsync();
        await table.Get();
        new Uri(this.server.LogEntries.Last().RequestMessage!.Url!).Query
            .Should().NotContain("id=", "a terminal clears the query it ran");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldThrowPostgrestException_GivenABodyThatIsNotJson()
    {
        this.RespondWith(200, "<html>proxy error</html>");
        var act = () => this.client.Table<Todo>().ExplainAsync();
        await act.Should().ThrowAsync<PostgrestException>("a success that cannot be read must not reach the caller as a plan");
    }

    [TestMethod]
    public async Task ExplainAsync_ShouldStopTheRequest_GivenACancelledToken()
    {
        this.RespondWith(200, PlanBody);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => this.client.Table<Todo>().ExplainAsync(cancellationToken: cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token reaches the HTTP call");
    }

    [TestMethod]
    public void ExplainAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.RespondWith(200, PlanBody);
        BlockingCaller.Completes(() => this.client.Table<Todo>().ExplainAsync())
            .Should().BeTrue("a library call must not capture the caller's synchronization context");
    }

    private void RespondWith(int status, string body) =>
        this.server.Given(Request.Create().WithPath("/todos").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
