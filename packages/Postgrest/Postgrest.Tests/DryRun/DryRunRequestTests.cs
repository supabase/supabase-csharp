using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.DryRun;

/// <summary>
///     <c>DryRun()</c> adds <c>tx=rollback</c> to the <c>Prefer</c> header of the next request, whatever its
///     terminal, next to the preferences that request already sends.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DryRunRequestTests
{
    private WireMockServer server = null!;
    private Client client = null!;

    [TestInitialize]
    public void SetUp()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().WithPath("/todos").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithHeader("Content-Range", "0-0/1").WithBody("""[{"id":1,"name":"walk the dog"}]"""));
        this.client = new Client(this.server.Url!, new ClientOptions());
    }

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task Insert_ShouldAppendTheRollbackPreference_GivenDryRun()
    {
        await this.client.Table<Todo>().DryRun().Insert(new Todo { Name = "walk the dog" });
        this.LastPreferHeader().Should().Be("return=representation,tx=rollback",
            "PostgREST rolls the transaction back on Prefer: tx=rollback, and the return entry must survive");
    }

    [TestMethod]
    public async Task SingleOrThrowAsync_ShouldSendOnlyTheRollbackPreference_GivenDryRun()
    {
        await this.client.Table<Todo>().DryRun().SingleOrThrowAsync();
        this.LastPreferHeader().Should().Be("tx=rollback", "a request without preferences of its own gets the header created");
    }

    [TestMethod]
    public async Task Count_ShouldAppendTheRollbackPreference_GivenDryRun()
    {
        await this.client.Table<Todo>().DryRun().Count(CountType.Exact);
        this.LastPreferHeader().Should().Be("count=exact,tx=rollback");
    }

    [TestMethod]
    public async Task Delete_ShouldKeepTheAffectedRowsLimit_GivenDryRunAndMaxAffected()
    {
        await this.client.Table<Todo>().Filter("done", Operator.Equals, "true").MaxAffected(2).DryRun().Delete();
        this.LastPreferHeader().Should().Be("return=representation,handling=strict,max-affected=2,tx=rollback");
    }

    [TestMethod]
    public async Task DryRun_ShouldOnlyApplyToTheNextRequest()
    {
        var table = this.client.Table<Todo>();
        await table.DryRun().Get();
        await table.Get();
        this.LastPreferHeader().Should().Be("count=estimated", "a later request commits as usual");
    }

    private string LastPreferHeader() => this.server.LogEntries.Last().RequestMessage!.Headers!["Prefer"].Single();
}
