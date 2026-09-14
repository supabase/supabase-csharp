using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using static Supabase.Postgrest.Constants;

namespace Postgrest.Tests.Reading;

/// <summary>
///     <c>Single()</c> fetches a list and enforces cardinality client-side, as postgrest-js's
///     <c>maybeSingle()</c> does. These pin, at the request/response level, that zero rows returns null,
///     one row returns the model, and more than one row throws — see issue #300.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SingleResponseTests
{
    private WireMockServer server = null!;
    private Client client = null!;

    [Table("todos")]
    private class Todo : BaseModel
    {
        [PrimaryKey("id")] public int Id { get; set; }
        [Column("name")] public string? Name { get; set; }
    }

    [TestInitialize]
    public void SetUp()
    {
        this.server = WireMockServer.Start();
        this.client = new Client(this.server.Url!, new ClientOptions());
    }

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task Single_ShouldReturnNull_GivenNoRowsMatch()
    {
        this.MockGetReturns("[]");
        var model = await this.client.Table<Todo>().Filter("id", Operator.Equals, "1").Single();
        model.Should().BeNull();
    }

    [TestMethod]
    public async Task Single_ShouldReturnTheModel_GivenExactlyOneRowMatches()
    {
        this.MockGetReturns("[{\"id\":1,\"name\":\"walk the dog\"}]");
        var model = await this.client.Table<Todo>().Filter("id", Operator.Equals, "1").Single();
        model!.Name.Should().Be("walk the dog");
    }

    [TestMethod]
    public async Task Single_ShouldThrow_GivenMoreThanOneRowMatches()
    {
        this.MockGetReturns("[{\"id\":1,\"name\":\"walk the dog\"},{\"id\":2,\"name\":\"walk the dog\"}]");
        var act = () => this.client.Table<Todo>().Filter("name", Operator.Equals, "walk the dog").Single();
        await act.Should().ThrowAsync<PostgrestException>(
            "returning null would hide an under-constrained query (issue #300)");
    }

    private void MockGetReturns(string body) =>
        this.server.Given(Request.Create().WithPath("/todos").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
}
