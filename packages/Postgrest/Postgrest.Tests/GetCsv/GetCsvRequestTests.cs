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
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.GetCsv;

/// <summary>
///     <c>GetCsvAsync</c> reads with the <c>text/csv</c> Accept media type, returns the body exactly as PostgREST
///     sends it, and raises PostgREST's refusals as <see cref="PostgrestException" />.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GetCsvRequestTests
{
    private const string CsvBody = "id,name\n1,walk the dog\n2,water the plants";

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
    public async Task GetCsvAsync_ShouldReturnTheBodyAsSent()
    {
        this.RespondWith(200, CsvBody);
        (await this.client.Table<Todo>().GetCsvAsync()).Should().Be(CsvBody, "format_csv returns the CSV text PostgREST produced, untouched");
    }

    [TestMethod]
    public async Task GetCsvAsync_ShouldSendGetWithTheCsvAcceptHeader()
    {
        this.RespondWith(200, CsvBody);
        await this.client.Table<Todo>().GetCsvAsync();
        var request = this.server.LogEntries.Single().RequestMessage!;
        (request.Method, request.Headers!["Accept"].Single())
            .Should().Be(("GET", "text/csv"), "PostgREST answers with CSV when the request accepts text/csv");
    }

    [TestMethod]
    public async Task GetCsvAsync_ShouldNotAskForNullsToBeStripped_GivenStripNulls()
    {
        this.RespondWith(200, CsvBody);
        await this.client.Table<Todo>().StripNulls().GetCsvAsync();
        this.server.LogEntries.Single().RequestMessage!.Headers!["Accept"].Single()
            .Should().Be("text/csv", "nulls=stripped only applies to JSON representations");
    }

    [TestMethod]
    public async Task GetCsvAsync_ShouldNotAskForACount()
    {
        this.RespondWith(200, CsvBody);
        await this.client.Table<Todo>().GetCsvAsync();
        this.server.LogEntries.Single().RequestMessage!.Headers!.ContainsKey("Prefer")
            .Should().BeFalse("the service owns the counting default, and CSV carries no count");
    }

    [TestMethod]
    public async Task GetCsvAsync_ShouldOnlyApplyToTheNextRequest()
    {
        this.RespondWith(200, "[]");
        var table = this.client.Table<Todo>();
        await table.GetCsvAsync();
        await table.Get();
        this.server.LogEntries.Last().RequestMessage!.Headers!.ContainsKey("Accept")
            .Should().BeFalse("a later read goes back to the JSON rows representation");
    }

    [TestMethod]
    public async Task GetCsvAsync_ShouldThrowNotAuthorized_GivenTheServerRefusesTheCaller()
    {
        this.RespondWith(401, """{"code":"42501","message":"permission denied for table todos"}""");
        var act = () => this.client.Table<Todo>().GetCsvAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason.Should().Be(FailureHint.Reason.NotAuthorized);
    }

    [TestMethod]
    public async Task GetCsvAsync_ShouldStopTheRequest_GivenACancelledToken()
    {
        this.RespondWith(200, CsvBody);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var act = () => this.client.Table<Todo>().GetCsvAsync(cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("the token reaches the HTTP call");
    }

    [TestMethod]
    public void GetCsvAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.RespondWith(200, CsvBody);
        BlockingCaller.Completes(() => this.client.Table<Todo>().GetCsvAsync())
            .Should().BeTrue("a library call must not capture the caller's synchronization context");
    }

    private void RespondWith(int status, string body) =>
        this.server.Given(Request.Create().WithPath("/todos").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "text/csv").WithBody(body));
}
