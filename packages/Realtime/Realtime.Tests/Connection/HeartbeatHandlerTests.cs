using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Connection;

/// <summary>
///     The client tells its heartbeat handlers how each heartbeat went: sent, answered, rejected or timed out.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class HeartbeatHandlerTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly List<(HeartbeatStatus Status, TimeSpan? Latency)> heard = [];
    private Client client = null!;

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    [DataRow("ok", HeartbeatStatus.Ok)]
    [DataRow("error", HeartbeatStatus.Error)]
    public async Task HeartbeatHandler_ShouldReportSentThenTheReply_GivenTheServerAnswers(string reply, HeartbeatStatus status)
    {
        var answered = this.Listen(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true, HeartbeatReplyStatus = reply }, until: status);
        await this.client.ConnectAsync();
        await answered.Task.WaitAsync(Patience);
        this.heard.Select(h => h.Status).Should().Equal([HeartbeatStatus.Sent, status], "a heartbeat is reported when sent and again when the server answers");
        this.heard[1].Latency.Should().BePositive("an answered heartbeat reports its round trip");
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldReportTimeout_GivenNoReply()
    {
        var timedOut = this.Listen(new FakeWebsocketClient { OpenOnStart = true }, until: HeartbeatStatus.Timeout);
        await this.client.ConnectAsync();
        await timedOut.Task.WaitAsync(Patience);
        this.heard.Select(h => h.Status).Should().Equal([HeartbeatStatus.Sent, HeartbeatStatus.Timeout], "an unanswered heartbeat is reported as a timeout");
    }

    [TestMethod]
    public async Task Connect_ShouldReportHeartbeats_GivenTheCallbackOverload()
    {
        var answered = this.Listen(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true }, until: HeartbeatStatus.Ok);
#pragma warning disable CS0618 // pins the still-shipping obsolete callback Connect
        this.client.Connect();
#pragma warning restore CS0618
        await answered.Awaiting(source => source.Task).Should().CompleteWithinAsync(Patience, "heartbeats are reported whichever way the client connected");
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true };
        this.client = ClientOver(socket);
        IRealtimeClient<RealtimeSocket, RealtimeChannel>.HeartbeatEventHandler handler = (_, status, latency) => this.heard.Add((status, latency));
        this.client.AddHeartbeatHandler(handler);
        this.client.RemoveHeartbeatHandler(handler);
        await this.client.ConnectAsync();
        await socket.FirstSend.Task.WaitAsync(Patience);
        this.heard.Should().BeEmpty("a removed handler is not notified");
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldNotBeCalled_GivenItWasCleared()
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true };
        this.client = ClientOver(socket);
        this.client.AddHeartbeatHandler((_, status, latency) => this.heard.Add((status, latency)));
        this.client.ClearHeartbeatHandlers();
        await this.client.ConnectAsync();
        await socket.FirstSend.Task.WaitAsync(Patience);
        this.heard.Should().BeEmpty("a cleared handler is not notified");
    }

    private TaskCompletionSource Listen(FakeWebsocketClient socket, HeartbeatStatus until)
    {
        var reached = new TaskCompletionSource();
        this.client = ClientOver(socket);
        this.client.AddHeartbeatHandler((_, status, latency) =>
        {
            if (reached.Task.IsCompleted) return;
            this.heard.Add((status, latency));
            if (status == until) reached.TrySetResult();
        });
        return reached;
    }

    private static Client ClientOver(FakeWebsocketClient socket) =>
        new("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket), HeartbeatInterval = TimeSpan.FromMilliseconds(50) });
}
