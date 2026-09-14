using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Requests;

/// <summary>
///     <see cref="ClientOptions.Retry" /> reaching the transport: a 503 is retried with a fresh body, and
///     the default options don't retry.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RetryTests
{
    private static readonly RetryOptions RetryOnce = new() { MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(1), UseJitter = false };

    private WireMockServer server = null!;

    [Table("todos")]
    private class Todo : BaseModel
    {
        [PrimaryKey("id")] public int Id { get; set; }
        [Column("name")] public string? Name { get; set; }
    }

    [TestInitialize]
    public void SetUp() => this.server = WireMockServer.Start();

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task Insert_ShouldRetryUntilSuccess_GivenRetryableStatus()
    {
        var client = new Client(this.server.Url!, new ClientOptions { Retry = RetryOnce });
        this.MockPostReturns503ThenCreated("[{\"id\":1,\"name\":\"walk the dog\"}]");

        var response = await client.Table<Todo>().Insert(new Todo { Name = "walk the dog" });

        response.Models.Should().ContainSingle().Which.Name.Should().Be("walk the dog");
    }

    [TestMethod]
    public async Task Insert_ShouldResendBody_GivenARetriedAttempt()
    {
        var client = new Client(this.server.Url!, new ClientOptions { Retry = RetryOnce });
        this.MockPostReturns503ThenCreated("[]");

        await client.Table<Todo>().Insert(new Todo { Name = "walk the dog" });

        var requests = this.server.LogEntries.ToList();
        requests.Should().HaveCount(2);
        requests[1].RequestMessage!.Body!.Should().Contain("walk the dog", "the retried attempt must resend the body");
    }

    [TestMethod]
    public async Task Insert_ShouldNotRetry_GivenDefaultOptions()
    {
        var client = new Client(this.server.Url!, new ClientOptions());
        this.server.Given(Request.Create().WithPath("/todos").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        var act = () => client.Table<Todo>().Insert(new Todo { Name = "walk the dog" });

        await act.Should().ThrowAsync<PostgrestException>();
        this.server.LogEntries.Should().ContainSingle("the default policy does not retry");
    }

    private void MockPostReturns503ThenCreated(string body)
    {
        this.server.Given(Request.Create().WithPath("/todos").UsingPost())
            .InScenario("retry").WillSetStateTo("retried")
            .RespondWith(Response.Create().WithStatusCode(503));
        this.server.Given(Request.Create().WithPath("/todos").UsingPost())
            .InScenario("retry").WhenStateIs("retried")
            .RespondWith(Response.Create().WithStatusCode(201).WithHeader("Content-Type", "application/json").WithBody(body));
    }
}
