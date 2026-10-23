using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Socket;

/// <summary>
///     An unanswered heartbeat means a dead connection, so the socket reconnects.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class HeartbeatTests
{
    private Client client = null!;

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public async Task Heartbeat_ShouldReconnect_GivenNoReply()
    {
        var socket = await this.Connect(answerHeartbeats: false);
        await socket.Awaiting(s => s.Reconnected.Task).Should().CompleteWithinAsync(TimeSpan.FromSeconds(5),
            "a connection that stops answering heartbeats is dead");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldKeepTheConnection_GivenReplies()
    {
        var socket = await this.Connect(answerHeartbeats: true);
        await socket.Awaiting(s => s.Reconnected.Task).Should().NotCompleteWithinAsync(TimeSpan.FromMilliseconds(500),
            "a healthy connection must not be reconnected");
    }

    private async Task<FakeWebsocketClient> Connect(bool answerHeartbeats)
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = answerHeartbeats };
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket), HeartbeatInterval = TimeSpan.FromMilliseconds(50) });
        await this.client.ConnectAsync();
        return socket;
    }
}
