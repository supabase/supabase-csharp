using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Errors;

/// <summary>
///     The <see cref="FailureHint.Reason" /> a <see cref="PostgrestException" /> carries for a 404 answer names the
///     kind of database object that could not be found, read from the Postgres SQLSTATE or the PostgREST schema
///     cache error code.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class FailureReasonTests
{
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
    public async Task Get_ShouldThrowUndefinedFunction_GivenPostgresReportsAnUndefinedFunction()
    {
        this.RespondWith404("""{"code":"42883","details":null,"hint":"No function matches the given name and argument types. You might need to add explicit type casts.","message":"function st_asgeojson(record) does not exist"}""");
        var act = () => this.client.Table<Todo>().Get();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.UndefinedFunction, "SQLSTATE 42883 is undefined_function");
    }

    [TestMethod]
    public async Task Get_ShouldThrowUndefinedTable_GivenPostgresReportsAnUndefinedTable()
    {
        this.RespondWith404("""{"code":"42P01","details":null,"hint":null,"message":"relation \"public.todos\" does not exist"}""");
        var act = () => this.client.Table<Todo>().Get();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.UndefinedTable, "SQLSTATE 42P01 is undefined_table");
    }

    [TestMethod]
    public async Task Get_ShouldThrowUndefinedTable_GivenTheSchemaCacheHasNoSuchTable()
    {
        this.RespondWith404("""{"code":"PGRST205","details":null,"hint":null,"message":"Could not find the table 'public.todos' in the schema cache"}""");
        var act = () => this.client.Table<Todo>().Get();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.UndefinedTable, "PostgREST reports a table missing from its schema cache as PGRST205");
    }

    [TestMethod]
    public async Task Rpc_ShouldThrowUndefinedFunction_GivenTheSchemaCacheHasNoSuchFunction()
    {
        this.server.Given(Request.Create().WithPath("/rpc/no_such_fn").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(404).WithHeader("Content-Type", "application/json")
                .WithBody("""{"code":"PGRST202","details":null,"hint":null,"message":"Could not find the function public.no_such_fn without parameters in the schema cache"}"""));
        var act = () => this.client.Rpc("no_such_fn", null);
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.UndefinedFunction, "PostgREST reports a function missing from its schema cache as PGRST202");
    }

    [TestMethod]
    public async Task Get_ShouldThrowUnknown_GivenAnotherNotFoundAnswer()
    {
        this.RespondWith404("""{"code":"PGRST125","details":null,"hint":null,"message":"Invalid path specified in request URL"}""");
        var act = () => this.client.Table<Todo>().Get();
        (await act.Should().ThrowAsync<PostgrestException>()).Which.Reason
            .Should().Be(FailureHint.Reason.Unknown, "a 404 that names no missing table or function says nothing about either");
    }

    private void RespondWith404(string body) =>
        this.server.Given(Request.Create().WithPath("/todos").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(404).WithHeader("Content-Type", "application/json").WithBody(body));
}
