using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Attaching;

/// <summary>
///     A model read from the server is attached to its table: it acts on its own row at the table's URL, with the
///     table's client options and headers, whether the server answered with an array of rows or a single object.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ReadModelAttachmentTests
{
    private const string Row = """{"id": 1, "name": "walk the dog"}""";

    private static readonly SentRequest TableScopedDelete = new("DELETE", "/todos", "anon-key", "Bearer user-token");

    private WireMockServer server = null!;

    [TestInitialize]
    public void SetUp() => this.server = WireMockServer.Start();

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task Get_ShouldAttachEachModelToTheTable_GivenAnArrayOfRows()
    {
        this.RespondWith($"[{Row}]");
        var models = (await this.Table().Get()).Models;
        await models.Single().Delete<Todo>();
        this.LastRequest().Should().Be(TableScopedDelete,
            "a model from an array of rows acts on its row with the table's client options and headers");
    }

    [TestMethod]
    public async Task Get_ShouldAttachTheModelToTheTable_GivenASingleObject()
    {
        this.RespondWith(Row);
        var model = (await this.Table().Get()).Model!;
        await model.Delete<Todo>();
        this.LastRequest().Should().Be(TableScopedDelete,
            "a model from a single object acts on its row with the table's client options and headers");
    }

    private IPostgrestTable<Todo> Table()
    {
        var options = new ClientOptions { Headers = new Dictionary<string, string> { ["apikey"] = "anon-key" } };
        var table = new Client(this.server.Url!, options).Table<Todo>();
        table.GetHeaders = () => new Dictionary<string, string> { ["Authorization"] = "Bearer user-token" };
        return table;
    }

    private SentRequest LastRequest()
    {
        var request = this.server.LogEntries.Last().RequestMessage!;
        return new SentRequest(request.Method, request.Path, request.Headers!["apikey"].Single(), request.Headers!["Authorization"].Single());
    }

    private void RespondWith(string body) =>
        this.server.Given(Request.Create().WithPath("/todos").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(body));

    private sealed record SentRequest(string Method, string Path, string ApiKey, string Authorization);
}
