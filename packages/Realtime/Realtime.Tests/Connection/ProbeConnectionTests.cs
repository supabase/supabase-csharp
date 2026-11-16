using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Connection;

/// <summary>
///     A probe checks the connection at once, when the app returns to the foreground, instead of waiting for the next heartbeat.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ProbeConnectionTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan AfterTheProbeTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan NoHeartbeatDue = TimeSpan.FromHours(1);
    private static readonly TimeSpan HeartbeatDueSoon = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AfterTheHeartbeatWasDue = TimeSpan.FromMilliseconds(1500);

    private readonly FakeWebsocketClient server = new() { OpenOnStart = true };
    private Client client = null!;

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public async Task ProbeConnection_ShouldSendAHeartbeatAtOnce_GivenAnOpenConnection()
    {
        await this.ConnectAndHear(HeartbeatStatus.Ok);
        var answered = this.Hear(HeartbeatStatus.Ok);
        this.client.ProbeConnection();
        await answered.Awaiting(a => a.Task).Should().CompleteWithinAsync(Patience, "the probe sends a heartbeat outside the heartbeat schedule");
    }

    [TestMethod]
    [DataRow(HeartbeatStatus.Ok)]
    [DataRow(HeartbeatStatus.Sent)]
    public async Task ProbeConnection_ShouldReconnect_GivenNoReplyBeforeTheDeadline(HeartbeatStatus beforeTheProbe)
    {
        await this.ConnectAndHear(beforeTheProbe);
        this.server.AnswerHeartbeats = false;
        this.client.ProbeConnection();
        await this.server.Awaiting(s => s.Reconnected.Task).Should().CompleteWithinAsync(Patience,
            "a probe without a reply is handled as a heartbeat timeout, whether it sent the heartbeat or found one waiting");
    }

    [TestMethod]
    public async Task ProbeConnection_ShouldSendNothingMoreAtTheDeadline_GivenAReplyBeforeIt()
    {
        await this.ConnectAndHear(HeartbeatStatus.Ok);
        var answered = this.Hear(HeartbeatStatus.Ok);
        this.client.ProbeConnection();
        await answered.Task.WaitAsync(Patience);
        var another = this.Hear(HeartbeatStatus.Sent);
        await another.Awaiting(a => a.Task).Should().NotCompleteWithinAsync(AfterTheProbeTimeout, "a reply before the deadline ends the probe");
    }

    [TestMethod]
    public async Task ProbeConnection_ShouldLeaveTheTimeoutToTheDeadline_GivenAHeartbeatComesDueBeforeIt()
    {
        await this.ConnectAndHear(HeartbeatStatus.Ok, HeartbeatDueSoon, probeTimeout: Patience);
        this.server.AnswerHeartbeats = false;
        this.client.ProbeConnection();
        await this.server.Awaiting(s => s.Reconnected.Task).Should().NotCompleteWithinAsync(AfterTheHeartbeatWasDue,
            "the next scheduled heartbeat would fail the socket before the probe deadline");
    }

    [TestMethod]
    public async Task ProbeConnection_ShouldSendNothing_GivenTheConnectionDropped()
    {
        await this.ConnectAndHear(HeartbeatStatus.Ok);
        this.server.Fail(new InvalidOperationException("boom"));
        var sent = this.Hear(HeartbeatStatus.Sent);
        this.client.ProbeConnection();
        await sent.Awaiting(s => s.Task).Should().NotCompleteWithinAsync(AfterTheProbeTimeout, "the probe runs only while the connection is connected");
    }

    [TestMethod]
    public async Task ProbeConnection_ShouldNotFailTheNewConnection_GivenTheSocketReopenedBeforeTheDeadline()
    {
        await this.ConnectAndHear(HeartbeatStatus.Ok);
        this.server.AnswerHeartbeats = false;
        var sent = this.Hear(HeartbeatStatus.Sent);
        this.client.ProbeConnection();
        await sent.Task.WaitAsync(Patience);
        this.server.RecoverFromLostConnection();
        await this.server.Awaiting(s => s.Reconnected.Task).Should().NotCompleteWithinAsync(AfterTheProbeTimeout,
            "the probed heartbeat belonged to the connection that is gone");
    }

    private async Task ConnectAndHear(HeartbeatStatus first, TimeSpan? heartbeatInterval = null, TimeSpan? probeTimeout = null)
    {
        this.server.AnswerHeartbeats = first == HeartbeatStatus.Ok;
        this.client = new Client("ws://localhost/socket", new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(this.server),
            HeartbeatInterval = heartbeatInterval ?? NoHeartbeatDue,
            ProbeTimeout = probeTimeout ?? ProbeTimeout
        });
        var heard = this.Hear(first);
        await this.client.ConnectAsync();
        await heard.Task.WaitAsync(Patience);
    }

    private TaskCompletionSource Hear(HeartbeatStatus expected)
    {
        var heard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.client.AddHeartbeatHandler((_, status, _) =>
        {
            if (status == expected) heard.TrySetResult();
        });
        return heard;
    }
}
