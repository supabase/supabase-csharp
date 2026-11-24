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
///     A push times out when the clock supplied through <see cref="ClientOptions.TimeProvider" /> says its timeout has passed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PushTimeoutOnSuppliedClockTests
{
    private const int TimeoutMs = 10_000;

    private readonly FakeTimeProvider clock = new();
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, AnswerHeartbeats = true };
    private Client client = null!;
    private RealtimeChannel channel = null!;

    [TestInitialize]
    public async Task Connect()
    {
        this.client = new Client("ws://localhost/socket",
            new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket), HeartbeatInterval = TimeSpan.FromDays(1), TimeProvider = this.clock });
        await this.client.ConnectAsync();
        this.channel = this.client.Channel("room");
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public void Push_ShouldTimeOut_GivenTheTimeoutPassedOnTheSuppliedClock()
    {
        var timeouts = 0;
        this.SentPush(() => timeouts++);
        this.clock.Advance(TimeSpan.FromMilliseconds(TimeoutMs));
        timeouts.Should().Be(1, "the timeout runs on the supplied clock");
    }

    [TestMethod]
    public void Push_ShouldWait_GivenTheSuppliedClockIsShortOfTheTimeout()
    {
        var timeouts = 0;
        this.SentPush(() => timeouts++);
        this.clock.Advance(TimeSpan.FromMilliseconds(TimeoutMs - 1));
        timeouts.Should().Be(0, "the push still has a millisecond left on the supplied clock");
    }

    [TestMethod]
    public void Push_ShouldTimeOutOnce_GivenTheClockKeepsRunning()
    {
        var timeouts = 0;
        this.SentPush(() => timeouts++);
        this.clock.Advance(TimeSpan.FromMilliseconds(TimeoutMs));
        this.clock.Advance(TimeSpan.FromMinutes(1));
        timeouts.Should().Be(1, "an unanswered push times out once and stops its timer");
    }

    [TestMethod]
    public void Push_ShouldTimeOutAfterTheNewTimeout_GivenAResend()
    {
        var timeouts = 0;
        var push = this.SentPush(() => timeouts++);
        push.Resend(2 * TimeoutMs);
        this.clock.Advance(TimeSpan.FromMilliseconds(TimeoutMs));
        timeouts.Should().Be(0, "a resend counts the timeout it was given, not the one the push started with");
    }

    [TestMethod]
    public void Push_ShouldNotTimeOut_GivenAReply()
    {
        var timeouts = 0;
        var push = this.SentPush(() => timeouts++);
        this.socket.Reply(this.channel.Topic, push.Ref);
        this.clock.Advance(TimeSpan.FromMilliseconds(TimeoutMs));
        timeouts.Should().Be(0, "a reply cancels the timeout");
    }

    [TestMethod]
    public void Push_ShouldNotThrow_GivenAThrowingTimeoutHandler()
    {
        this.SentPush(() => throw new InvalidOperationException("handler failed"));
        var act = () => this.clock.Advance(TimeSpan.FromMilliseconds(TimeoutMs));
        act.Should().NotThrow("a timer callback that throws takes the process down, so the push must contain what a handler throws");
    }

    private Push SentPush(Action onTimeout)
    {
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken, timeoutMs: TimeoutMs);
        push.OnTimeout += (_, _) => onTimeout();
        push.Send();
        return push;
    }
}
