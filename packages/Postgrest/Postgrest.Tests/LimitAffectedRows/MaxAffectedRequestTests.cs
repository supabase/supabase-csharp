using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.LimitAffectedRows;

/// <summary>
///     <c>MaxAffected</c> sends <c>Prefer: handling=strict,max-affected=N</c> on Update and Delete, next to the
///     entries the call already sends, and maps PostgREST's <c>PGRST124</c> answer to
///     <see cref="FailureHint.Reason.MaxAffectedExceeded" />.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class MaxAffectedRequestTests
{
    private const string ExceededBody =
        """{"code":"PGRST124","details":"Results contain 3 rows","hint":null,"message":"Query result exceeds max-affected preference constraint"}""";

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
    public async Task Delete_ShouldSendStrictMaxAffectedPreference_GivenMaxAffected()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingDelete()).RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));
        await this.client.Table<Todo>().Filter("done", Operator.Equals, "true").MaxAffected(2).Delete();
        this.server.LogEntries.Last().RequestMessage!.Headers!["Prefer"].Single()
            .Should().Be("return=representation,handling=strict,max-affected=2",
                "PostgREST 13 enforces max-affected only together with handling=strict, and the return entry must survive");
    }

    [TestMethod]
    public async Task Update_ShouldSendStrictMaxAffectedPreference_GivenMaxAffected()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingPatch()).RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));
        await this.client.Table<Todo>().Filter("done", Operator.Equals, "false").Set(x => x.Done, true).MaxAffected(1).Update();
        this.server.LogEntries.Last().RequestMessage!.Headers!["Prefer"].Single()
            .Should().Be("return=representation,handling=strict,max-affected=1");
    }

    [TestMethod]
    public async Task Delete_ShouldSendMaxAffectedZero_GivenZero()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingDelete()).RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));
        await this.client.Table<Todo>().MaxAffected(0).Delete();
        this.server.LogEntries.Last().RequestMessage!.Headers!["Prefer"].Single()
            .Should().EndWith("max-affected=0", "zero is a valid limit that forbids any affected row");
    }

    [TestMethod]
    public async Task Delete_ShouldSendOnlyTheLimitPreference_GivenNoOtherPreference()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingDelete()).RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));
        await this.client.Table<Todo>().MaxAffected(4).Delete(new QueryOptions { Returning = QueryOptions.ReturnType.Minimal });
        this.server.LogEntries.Last().RequestMessage!.Headers!["Prefer"].Single()
            .Should().Be("return=minimal,handling=strict,max-affected=4");
    }

    [TestMethod]
    public async Task Delete_ShouldThrowMaxAffectedExceeded_GivenPostgrestRefusesTheMutation()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(400).WithBody(ExceededBody));
        var act = () => this.client.Table<Todo>().MaxAffected(1).Delete();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.MaxAffectedExceeded, "PGRST124 is PostgREST's answer to a breached max-affected limit");
    }

    [TestMethod]
    public async Task Delete_ShouldNotReportMaxAffectedExceeded_GivenAnotherBadRequest()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(400).WithBody("""{"code":"PGRST100"}"""));
        var act = () => this.client.Table<Todo>().Delete();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason.Should().Be(FailureHint.Reason.Unknown);
    }

    [TestMethod]
    public async Task Delete_ShouldNotSendTheLimit_GivenTheTableIsReusedAfterTheMutation()
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingDelete()).RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));
        var table = this.client.Table<Todo>().MaxAffected(1);
        await table.Delete();
        await table.Delete();
        this.server.LogEntries.Last().RequestMessage!.Headers!["Prefer"].Single()
            .Should().NotContain("max-affected", "the limit belongs to one query and is cleared with the rest of its state");
    }
}
