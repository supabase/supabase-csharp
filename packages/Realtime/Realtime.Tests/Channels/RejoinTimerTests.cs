using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Channel;

namespace Realtime.Tests.Channels;

/// <summary>
///     The rejoin timer's schedule on a fake clock: asking a running timer to start leaves its countdown alone,
///     a stopped one starts fresh, and a join reply restarts the countdown from the reply.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class RejoinTimerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    private readonly FakeTimeProvider clock = new();
    private SpyTimeProvider spy = null!;

    [TestInitialize]
    public void Init() => this.spy = new SpyTimeProvider(this.clock);

    [TestMethod]
    public void RejoinTimer_ShouldKeepItsCountdown_GivenAnotherJoinMidInterval()
    {
        var channel = this.UnconnectedChannel();
        channel.Rejoin(timeoutMs: 600_000);
        this.clock.Advance(Timeout / 2);
        channel.Rejoin(timeoutMs: 600_000);
        this.clock.Advance(Timeout / 2);
        this.RejoinTicks().Should().Be(1, "a state change mid-interval must not postpone the next tick");
    }

    [TestMethod]
    public void RejoinTimer_ShouldTickAgain_GivenAJoinAfterTheChannelClosed()
    {
        var channel = this.UnconnectedChannel();
        channel.Rejoin(timeoutMs: 600_000);
        channel.Unsubscribe();
        channel.Rejoin(timeoutMs: 600_000);
        this.clock.Advance(Timeout);
        this.RejoinTicks().Should().Be(1, "closing the channel stops the timer, so the next join starts it again");
    }

    [TestMethod]
    public async Task RejoinTimer_ShouldRestartFromTheReply_GivenAnOkJoinReply()
    {
        var (socket, channel) = await this.Connected();
        var subscribe = channel.Subscribe();
        this.clock.Advance(Timeout / 2);
        socket.Receive(JoinReply(channel, "ok"));
        await subscribe;
        this.clock.Advance(Timeout / 2);
        this.RejoinTicks().Should().Be(0, "the reply stops the timer; it restarts a full interval after the reply");
        this.clock.Advance(Timeout / 2);
        this.RejoinTicks().Should().Be(1);
    }

    [TestMethod]
    public async Task RejoinTimer_ShouldRestartFromTheReply_GivenAnErrorJoinReply()
    {
        var (socket, channel) = await this.Connected();
        var subscribe = channel.Subscribe();
        this.clock.Advance(Timeout / 2);
        socket.Receive(JoinReply(channel, "error"));
        await subscribe.Awaiting(s => s).Should().ThrowAsync<Exception>();
        this.clock.Advance(Timeout / 2);
        this.RejoinTicks().Should().Be(0, "the reply stops the timer; it restarts a full interval after the reply");
        this.clock.Advance(Timeout / 2);
        this.RejoinTicks().Should().Be(1);
    }

    private int RejoinTicks() => this.spy.TimersOwnedBy<RealtimeChannel>().Single().Ticks;

    private RealtimeChannel UnconnectedChannel()
    {
        var options = new ClientOptions { Timeout = Timeout, TimeProvider = this.spy };
        return Wire.Channel(options: ChannelOptions.Public(options, () => null, Wire.Settings()));
    }

    private async Task<(FakeWebsocketClient Socket, RealtimeChannel Channel)> Connected()
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true };
        var client = new Client("ws://localhost/socket",
            new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket), TimeProvider = this.spy, Timeout = Timeout });
        await client.ConnectAsync();
        // Until the first heartbeat is out, the socket takes frames with a null ref for heartbeat replies.
        await socket.FirstSend.Task;
        return (socket, client.Channel("room"));
    }

    private static string JoinReply(RealtimeChannel channel, string status) =>
        $$$"""{"topic":"realtime:room","event":"phx_reply","ref":"{{{channel.JoinPush!.Ref}}}","payload":{"status":"{{{status}}}","response":{} } }""";
}
