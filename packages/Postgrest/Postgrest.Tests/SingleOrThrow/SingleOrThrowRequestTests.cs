using System.Linq;
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
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.SingleOrThrow;

/// <summary>
///     <c>SingleOrThrowAsync</c> asks PostgREST for a single object with the
///     <c>application/vnd.pgrst.object+json</c> Accept media type, keeping <c>nulls=stripped</c> when
///     <c>StripNulls()</c> is set, and maps the 406 <c>PGRST116</c> answer to
///     <see cref="FailureHint.Reason.UnexpectedRowCount" />.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SingleOrThrowRequestTests
{
    private const string RowBody = """{"id":1,"name":"walk the dog"}""";

    private const string NoRowBody =
        """{"code":"PGRST116","details":"The result contains 0 rows","hint":null,"message":"Cannot coerce the result to a single JSON object"}""";

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
    public async Task SingleOrThrowAsync_ShouldReturnTheRow_GivenTheServerAnswersAnObject()
    {
        this.RespondWith(200, RowBody);
        (await this.client.Table<Todo>().SingleOrThrowAsync()).Name.Should().Be("walk the dog");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldSendGetWithTheSingularAcceptHeader()
    {
        this.RespondWith(200, RowBody);
        await this.client.Table<Todo>().Filter("id", Operator.Equals, "1").SingleOrThrowAsync();
        var request = this.server.LogEntries.Single().RequestMessage!;
        (request.Method, request.Headers!["Accept"].Single())
            .Should().Be(("GET", "application/vnd.pgrst.object+json"), "the singular media type makes PostgREST enforce exactly one row");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldKeepNullsStripped_GivenStripNulls()
    {
        this.RespondWith(200, RowBody);
        await this.client.Table<Todo>().StripNulls().SingleOrThrowAsync();
        this.server.LogEntries.Single().RequestMessage!.Headers!["Accept"].Single()
            .Should().Be("application/vnd.pgrst.object+json;nulls=stripped");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldNotStripNulls_GivenALaterQueryOnTheSameTable()
    {
        this.RespondWith(200, RowBody);
        var table = this.client.Table<Todo>();
        await table.StripNulls().SingleOrThrowAsync();
        await table.SingleOrThrowAsync();
        this.server.LogEntries.Select(entry => entry.RequestMessage!.Headers!["Accept"].Single())
            .Should().Equal("application/vnd.pgrst.object+json;nulls=stripped", "application/vnd.pgrst.object+json");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldOnlyApplyToTheNextRequest()
    {
        this.RespondWith(200, RowBody);
        var table = this.client.Table<Todo>();
        await table.SingleOrThrowAsync();
        await table.Get();
        this.server.LogEntries.Last().RequestMessage!.Headers!.ContainsKey("Accept")
            .Should().BeFalse("a later read goes back to the plural representation");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldThrowUnexpectedRowCount_GivenPgrst116()
    {
        this.RespondWith(406, NoRowBody);
        var act = () => this.client.Table<Todo>().SingleOrThrowAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.UnexpectedRowCount, "single_row delegates cardinality to PostgREST as PGRST116 on every method");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldNotReportUnexpectedRowCount_GivenAnotherNotAcceptableAnswer()
    {
        this.RespondWith(406, """{"code":"PGRST107","message":"None of these media types are available"}""");
        var act = () => this.client.Table<Todo>().SingleOrThrowAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason.Should().Be(FailureHint.Reason.MediaTypeNotAcceptable);
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldThrowUnexpectedRowCount_GivenSuccessWithoutRow()
    {
        this.RespondWith(200, "[]");
        var act = () => this.client.Table<Todo>().SingleOrThrowAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Should().Match<PostgrestException>(
            exception => exception.Reason == FailureHint.Reason.UnexpectedRowCount && exception.StatusCode == 406,
            "a success that carries no row must never reach the caller as a null row");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldThrowUnexpectedRowCount_GivenSuccessWithSeveralRows()
    {
        this.RespondWith(200, "[" + RowBody + "," + RowBody + "]");
        var act = () => this.client.Table<Todo>().SingleOrThrowAsync();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Should().Match<PostgrestException>(
            exception => exception.Reason == FailureHint.Reason.UnexpectedRowCount && exception.Message == "The query matched 2 rows when exactly one was expected.");
    }

    [TestMethod]
    public void SingleOrThrowAsync_ShouldNotResumeOnTheCallersContext()
    {
        this.RespondWith(200, RowBody);
        BlockingCaller.Completes(() => this.client.Table<Todo>().SingleOrThrowAsync())
            .Should().BeTrue("a library call must not capture the caller's synchronization context");
    }

    private void RespondWith(int status, string body) =>
        this.server.Given(Request.Create().WithPath("/todos").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json").WithBody(body));
}
