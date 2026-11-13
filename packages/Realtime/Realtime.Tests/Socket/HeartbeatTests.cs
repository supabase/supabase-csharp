using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
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
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

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

    [TestMethod]
    public async Task Heartbeat_ShouldSendOnEachInterval_GivenACustomTimeProvider()
    {
        var clock = new FakeTimeProvider();
        var socket = await this.Connect(answerHeartbeats: true, clock);
        await HeartbeatsSent(socket, 1, "the first heartbeat goes out as soon as the socket opens");
        clock.Advance(Interval - TimeSpan.FromTicks(1));
        await Task.Delay(100);
        socket.HeartbeatsSent.Should().Be(1, "the next heartbeat waits a full interval on the supplied clock");
        clock.Advance(TimeSpan.FromTicks(1));
        await HeartbeatsSent(socket, 2, "a heartbeat is due every interval on the supplied clock");
        clock.Advance(Interval);
        await HeartbeatsSent(socket, 3, "the heartbeat keeps repeating");
        socket.Reconnected.Task.IsCompleted.Should().BeFalse("every heartbeat was answered");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldReconnect_GivenNoReplyWithinAnIntervalOnACustomTimeProvider()
    {
        var clock = new FakeTimeProvider();
        var socket = await this.Connect(answerHeartbeats: false, clock);
        await HeartbeatsSent(socket, 1, "the first heartbeat goes out as soon as the socket opens");
        socket.Reconnected.Task.IsCompleted.Should().BeFalse("the unanswered heartbeat is still within its interval");
        clock.Advance(Interval);
        await socket.Awaiting(s => s.Reconnected.Task).Should().CompleteWithinAsync(TimeSpan.FromSeconds(5),
            "a heartbeat still unanswered when the next is due on the supplied clock means a dead connection");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldReplaceTheRunningTimer_GivenAReconnect()
    {
        var spy = new SpyTimeProvider(new FakeTimeProvider());
        var socket = await this.Connect(answerHeartbeats: true, spy);
        socket.RecoverFromLostConnection();
        var timers = spy.TimersOwnedBy<RealtimeSocket>();
        timers.Should().HaveCount(2, "each open starts a fresh heartbeat");
        timers[0].IsDisposed.Should().BeTrue("the old heartbeat must stop, or every reconnect doubles the heartbeats");
        timers[1].IsDisposed.Should().BeFalse();
    }

    [TestMethod]
    public async Task Disconnect_ShouldStopTheHeartbeat()
    {
        var spy = new SpyTimeProvider(new FakeTimeProvider());
        await this.Connect(answerHeartbeats: true, spy);
        this.client.Socket!.Disconnect();
        spy.TimersOwnedBy<RealtimeSocket>().Single().IsDisposed.Should().BeTrue("a disconnected socket sends no heartbeats");
    }

    [TestMethod]
    public async Task Dispose_ShouldStopTheHeartbeat()
    {
        var spy = new SpyTimeProvider(new FakeTimeProvider());
        await this.Connect(answerHeartbeats: true, spy);
        ((IDisposable) this.client.Socket!).Dispose();
        spy.TimersOwnedBy<RealtimeSocket>().Single().IsDisposed.Should().BeTrue("a disposed socket sends no heartbeats");
    }

    private async Task<FakeWebsocketClient> Connect(bool answerHeartbeats, TimeProvider? clock = null)
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = answerHeartbeats };
        var options = new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket), HeartbeatInterval = TimeSpan.FromMilliseconds(50) };
        if (clock != null)
        {
            options.HeartbeatInterval = Interval;
            options.TimeProvider = clock;
        }
        this.client = new Client("ws://localhost/socket", options);
        await this.client.ConnectAsync();
        return socket;
    }

    /// <summary>Heartbeat frames go out on a background task, so wait (bounded) for the count to land.</summary>
    private static async Task HeartbeatsSent(FakeWebsocketClient socket, int expected, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (socket.HeartbeatsSent < expected && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        socket.HeartbeatsSent.Should().Be(expected, because);
    }
}
