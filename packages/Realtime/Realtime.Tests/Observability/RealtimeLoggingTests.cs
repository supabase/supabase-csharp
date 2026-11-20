using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Observability;

/// <summary>
/// What the client tells a logger: the socket's lifecycle (connected, disconnected, reconnecting, heartbeat timeout) and every
/// channel's state changes at Debug, and the failure of a background token refresh at Warning, without the endpoint, tokens or
/// payloads (client.observability.diagnostic_logging, realtime.configuration.custom_logger).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class RealtimeLoggingTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly LogCapture capture = new();
    private Client client = null!;

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    private Client ClientOver(FakeWebsocketClient socket, Func<Task<string?>>? retrieveAccessToken = null) =>
        this.client = new Client("ws://localhost/socket?apikey=secret-key", new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(socket),
            HeartbeatInterval = TimeSpan.FromMilliseconds(50),
            Logger = this.capture.Logger,
            RetrieveAccessToken = retrieveAccessToken
        });

    private string[] Messages() => this.capture.Records.Select(record => record.Message).ToArray();

    [TestMethod]
    public async Task ConnectAsync_ShouldLogTheConnectionAtDebug()
    {
        await this.ClientOver(new FakeWebsocketClient { OpenOnStart = true }).ConnectAsync();
        this.Messages().Should().Contain("Supabase.Realtime: connected");
    }

    [TestMethod]
    public async Task Channel_ShouldLogItsStateAtDebug_GivenItJoined()
    {
        var realtime = this.ClientOver(new FakeWebsocketClient { OpenOnStart = true, AnswerJoins = true });
        await realtime.ConnectAsync();
        await realtime.Channel("room").Subscribe();
        this.Messages().Should().Contain("Supabase.Realtime: channel realtime:room Joined", "a channel's state change is a lifecycle diagnostic");
    }

    [TestMethod]
    public async Task Socket_ShouldNeverLogTheEndpointOrItsApiKey()
    {
        await this.ClientOver(new FakeWebsocketClient { OpenOnStart = true }).ConnectAsync();
        this.capture.Records.Should().NotContain(record => record.Message.Contains("secret-key") || record.Message.Contains("localhost"),
            "the endpoint carries the API key in its query string");
    }

    [TestMethod]
    public async Task Socket_ShouldLogTheDropAndTheReconnection_GivenALostConnectionCameBack()
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true };
        await this.ClientOver(socket).ConnectAsync();
        socket.Fail(new InvalidOperationException("boom"));
        socket.RecoverFromLostConnection();
        using (new AssertionScope())
        {
            this.Messages().Should().Contain(message => message.StartsWith("Supabase.Realtime: disconnected", StringComparison.Ordinal), "the drop is a lifecycle diagnostic");
            this.Messages().Should().Contain("Supabase.Realtime: reconnection attempt scheduled", "the next attempt is a lifecycle diagnostic");
            this.Messages().Should().Contain(message => message.StartsWith("Supabase.Realtime: reconnection ", StringComparison.Ordinal) && !message.EndsWith("scheduled", StringComparison.Ordinal),
                "the recovery is a lifecycle diagnostic");
        }
    }

    [TestMethod]
    public async Task Socket_ShouldLogTheHeartbeatTimeoutAtDebug_GivenNoReply()
    {
        await this.ClientOver(new FakeWebsocketClient { OpenOnStart = true }).ConnectAsync();
        await WaitUntil(() => this.Messages().Contains("Supabase.Realtime: heartbeat timed out, reconnecting"));
        this.Messages().Should().Contain("Supabase.Realtime: heartbeat timed out, reconnecting");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldLogAWarning_GivenTheTokenCallbackThrows()
    {
        var calls = 0;
        await this.ClientOver(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true },
            () => ++calls == 1 ? Task.FromResult<string?>("first") : throw new InvalidOperationException("secret-detail")).ConnectAsync();
        await WaitUntil(() => this.capture.Records.Any(record => record.Level == LogLevel.Warning));
        this.capture.Records.First(record => record.Level == LogLevel.Warning).Message.Should()
            .Be("Supabase.Realtime: Access token refresh on heartbeat failed with InvalidOperationException", "a heartbeat has no caller to throw to");
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }
}
