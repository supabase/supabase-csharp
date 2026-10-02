using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Postgrest.Tests.Requests;

/// <summary>
///     The HTTP transport a <see cref="Client" /> sends through: a caller-supplied <see cref="ClientOptions.HttpClient" />
///     is used verbatim instead of the client building its own — both for <see cref="Table{TModel}" /> requests and
///     for <see cref="Client.Rpc(string, object)" />, which routes through the client's own resolved transport
///     rather than a table. The client built for a <see cref="ClientOptions.Proxy" /> is reused across tables.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class HttpClientInjectionTests
{
    private WireMockServer server = null!;

    [Table("todos")]
    private class Todo : BaseModel
    {
        [PrimaryKey("id", false)] public int Id { get; set; }
        [Column("name")] public string? Name { get; set; }
    }

    [TestInitialize]
    public void SetUp() => this.server = WireMockServer.Start();

    [TestCleanup]
    public void TearDown() => this.server.Stop();

    [TestMethod]
    public async Task Get_ShouldSendThroughTheInjectedHttpClient_GivenClientOptions()
    {
        using var injectedClient = new HttpClient();
        injectedClient.DefaultRequestHeaders.Add("X-Injected", "true");
        var client = new Client(this.server.Url!, new ClientOptions { HttpClient = injectedClient });

        this.server.Given(Request.Create().WithPath("/todos").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));

        await client.Table<Todo>().Get();

        this.server.LogEntries.Should().ContainSingle()
            .Which.RequestMessage!.Headers!["X-Injected"][0].Should().Be("true",
                "the client must send through the caller-supplied HttpClient, not build its own");
    }

    [TestMethod]
    public async Task Rpc_ShouldSendThroughTheInjectedHttpClient_GivenClientOptions()
    {
        using var injectedClient = new HttpClient();
        injectedClient.DefaultRequestHeaders.Add("X-Injected", "true");
        var client = new Client(this.server.Url!, new ClientOptions { HttpClient = injectedClient });

        this.server.Given(Request.Create().WithPath("/rpc/echo").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));

        await client.Rpc("echo", new { });

        this.server.LogEntries.Should().ContainSingle()
            .Which.RequestMessage!.Headers!["X-Injected"][0].Should().Be("true",
                "Rpc must route through the client's own resolved transport, not a separate default");
    }

    [TestMethod]
    public async Task Get_ShouldReuseTheProxyConnection_GivenASecondTable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var connections = 0;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var connection = await listener.AcceptTcpClientAsync();
                connections++;
                _ = AnswerGetRequests(connection);
            }
        });
        var proxy = new WebProxy($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}");
        var client = new Client("http://postgrest.test", new ClientOptions { Proxy = proxy });

        try
        {
            await client.Table<Todo>().Get();
            await client.Table<Todo>().Get();

            connections.Should().Be(1, "a new HttpClient per table opens a new connection each time (issue #450)");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task AnswerGetRequests(TcpClient connection)
    {
        var stream = connection.GetStream();
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync() is { } line)
        {
            // A GET has no body, so the blank line ends the request.
            if (line.Length == 0)
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n[]"));
        }
    }
}
