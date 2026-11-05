using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Channels;

/// <summary>
///     A push the server never answers, like <c>access_token</c>, times out once and stops listening.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PushTimeoutTests
{
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true };
    private Client client = null!;
    private RealtimeChannel channel = null!;

    [TestInitialize]
    public async Task Connect()
    {
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await this.client.ConnectAsync();
        this.channel = this.client.Channel("room");
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public async Task Push_ShouldTimeOutOnce_GivenNoReply()
    {
        var timeouts = 0;
        var push = await this.TimedOutPush();
        push.OnTimeout += (_, _) => timeouts++;
        await Task.Delay(300);
        timeouts.Should().Be(0, "an unanswered push must not keep its timer running");
    }

    [TestMethod]
    public async Task Push_ShouldIgnoreALateReply_GivenItTimedOut()
    {
        var push = await this.TimedOutPush();
        this.socket.Reply(this.channel.Topic, push.Ref);
        push.Response.Should().BeNull("a timed out push must stop listening to the socket");
    }

    [TestMethod]
    public async Task Push_ShouldReceiveTheReply_GivenAResendAfterTimeout()
    {
        var push = await this.TimedOutPush();
        push.Resend(5000);
        this.socket.Reply(this.channel.Topic, push.Ref);
        push.Response.Should().NotBeNull();
    }

    [TestMethod]
    public void Push_ShouldTimeOutOnce_GivenTheTimeoutElapsesOnACustomTimeProvider()
    {
        var clock = new FakeTimeProvider();
        var clockedChannel = Wire.Channel(options: ChannelOptions.Public(new ClientOptions { TimeProvider = clock }, () => null, Wire.Settings()));
        var timeouts = 0;
        var push = new Push(clockedChannel.Socket, clockedChannel, ChannelAccessToken, timeoutMs: 10_000);
        push.OnTimeout += (_, _) => timeouts++;
        push.Send();
        clock.Advance(TimeSpan.FromMilliseconds(9_999));
        timeouts.Should().Be(0, "the push still has a millisecond left on the supplied clock");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        timeouts.Should().Be(1, "the timeout runs on the supplied clock");
        clock.Advance(TimeSpan.FromMinutes(1));
        timeouts.Should().Be(1, "the push timeout is one-shot");
    }

    private async Task<Push> TimedOutPush()
    {
        var timedOut = new TaskCompletionSource();
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken, timeoutMs: 50);
        push.OnTimeout += (_, _) => timedOut.TrySetResult();
        push.Send();
        await timedOut.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return push;
    }
}
