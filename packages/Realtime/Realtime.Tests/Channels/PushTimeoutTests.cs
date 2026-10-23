using System;
using System.Threading.Tasks;
using FluentAssertions;
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
    private RealtimeChannel channel = null!;

    [TestInitialize]
    public async Task Connect()
    {
        var client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await client.ConnectAsync();
        this.channel = client.Channel("room");
    }

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
        this.Reply(push.Ref);
        push.Response.Should().BeNull("a timed out push must stop listening to the socket");
    }

    [TestMethod]
    public async Task Push_ShouldReceiveTheReply_GivenAResendAfterTimeout()
    {
        var push = await this.TimedOutPush();
        push.Resend(5000);
        this.Reply(push.Ref);
        push.Response.Should().NotBeNull();
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

    private void Reply(string? reference) =>
        this.socket.Receive("""{"topic":"realtime:room","event":"phx_reply","ref":"REF","payload":{"status":"ok","response":{}}}""".Replace("REF", reference));
}
