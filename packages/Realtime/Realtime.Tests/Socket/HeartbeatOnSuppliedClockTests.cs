using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Socket;

/// <summary>
///     The heartbeat waits on <see cref="ClientOptions.TimeProvider" />: it goes out when the supplied clock says
///     an interval has passed, however little real time has.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class HeartbeatOnSuppliedClockTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider clock = new();
    private Client client = null!;

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public async Task Heartbeat_ShouldSendAtOnce_GivenTheSocketOpened()
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true };
        var first = socket.SentFrame("heartbeat", 1);
        await this.Connect(socket);
        await first.Awaiting(frame => frame).Should().CompleteWithinAsync(Patience, "the first heartbeat does not wait for an interval");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldSendAgain_GivenAnIntervalPassedOnTheSuppliedClock()
    {
        var socket = await this.ConnectAfterTheFirstHeartbeat(answerHeartbeats: true);
        var second = socket.SentFrame("heartbeat", 2);
        this.clock.Advance(Interval);
        await second.Awaiting(frame => frame).Should().CompleteWithinAsync(Patience, "an interval on the supplied clock is due another heartbeat");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldWait_GivenTheSuppliedClockIsShortOfAnInterval()
    {
        var socket = await this.ConnectAfterTheFirstHeartbeat(answerHeartbeats: true);
        var second = socket.SentFrame("heartbeat", 2);
        this.clock.Advance(Interval - TimeSpan.FromTicks(1));
        second.IsCompleted.Should().BeFalse("the next heartbeat waits a full interval on the supplied clock");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldReconnect_GivenNoReplyBeforeTheNextInterval()
    {
        var socket = await this.ConnectAfterTheFirstHeartbeat(answerHeartbeats: false);
        this.clock.Advance(Interval);
        await socket.Awaiting(s => s.Reconnected.Task).Should().CompleteWithinAsync(Patience, "a heartbeat still unanswered when the next is due means a dead connection");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldStop_GivenTheClientDisconnected()
    {
        var socket = await this.ConnectAfterTheFirstHeartbeat(answerHeartbeats: true);
        var second = socket.SentFrame("heartbeat", 2);
        this.client.Disconnect();
        this.clock.Advance(Interval * 3);
        second.IsCompleted.Should().BeFalse("a disconnected client sends no heartbeats");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldNotRunTwice_GivenTheSocketOpenedAgain()
    {
        var socket = await this.ConnectAfterTheFirstHeartbeat(answerHeartbeats: true);
        var second = socket.SentFrame("heartbeat", 2);
        var third = socket.SentFrame("heartbeat", 3);
        var fourth = socket.SentFrame("heartbeat", 4);
        socket.RecoverFromLostConnection();
        await second.WaitAsync(Patience);
        this.clock.Advance(Interval);
        await third.WaitAsync(Patience);
        fourth.IsCompleted.Should().BeFalse("each open replaces the heartbeat, so a reconnect must not double it");
    }

    private async Task<FakeWebsocketClient> ConnectAfterTheFirstHeartbeat(bool answerHeartbeats)
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = answerHeartbeats };
        var first = socket.SentFrame("heartbeat", 1);
        await this.Connect(socket);
        await first.WaitAsync(Patience);
        return socket;
    }

    private async Task Connect(FakeWebsocketClient socket)
    {
        this.client = new Client("ws://localhost/socket",
            new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket), HeartbeatInterval = Interval, TimeProvider = this.clock });
        await this.client.ConnectAsync();
    }
}
