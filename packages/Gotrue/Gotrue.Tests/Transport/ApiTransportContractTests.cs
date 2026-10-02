using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Core.Http;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Gotrue.Tests.Transport;

/// <summary>Covers injectable HttpClient, retry-policy wiring and proxy client reuse in the transport layer.</summary>
[TestClass]
[TestCategory("Contract")]
public class ApiTransportContractTests
{
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task RefreshToken_ShouldSendThroughTheInjectedHttpClient_GivenClientOptions()
    {
        using var injectedClient = new HttpClient();
        injectedClient.DefaultRequestHeaders.Add("X-Injected", "true");
        var client = new Client(new ClientOptions { Url = this.server.Url, AllowUnconfirmedUserSessions = true, HttpClient = injectedClient });
        this.MockTokenSuccess();

        await client.RefreshToken("access", "refresh");

        this.server.VerifySingleReceivedRequest().WithHeader("X-Injected", "true");
        client.Shutdown();
    }

    [TestMethod]
    public async Task RefreshToken_ShouldRetryUntilSuccess_GivenRetryableStatus()
    {
        var client = new Client(new ClientOptions
        {
            Url = this.server.Url,
            AllowUnconfirmedUserSessions = true,
            Retry = new RetryOptions { MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(1) },
        });
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .InScenario("retry").WillSetStateTo("retried")
            .RespondWith(Response.Create().WithStatusCode(503));
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .InScenario("retry").WhenStateIs("retried")
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"new-token\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600,\"token_type\":\"bearer\"}"));

        await client.RefreshToken("access", "refresh");

        client.CurrentSession!.AccessToken.Should().Be("new-token", "the retryable 503 should be retried until the second response succeeds");
        client.Shutdown();
    }

    [TestMethod]
    public async Task Settings_ShouldReuseTheProxyConnection_GivenASecondStatelessCall()
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
        var options = new StatelessClient.StatelessClientOptions
        {
            Url = "http://gotrue.test",
            Proxy = new WebProxy($"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
        };
        var client = new StatelessClient();

        try
        {
            await client.Settings(options);
            await client.Settings(options);

            connections.Should().Be(1, "a new HttpClient per stateless call opens a new connection each time (issue #450)");
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
            // A blank line ends a GET request.
            if (line.Length == 0)
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}"));
        }
    }

    private void MockTokenSuccess() =>
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("{\"access_token\":\"new-token\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600,\"token_type\":\"bearer\"}"));
}
