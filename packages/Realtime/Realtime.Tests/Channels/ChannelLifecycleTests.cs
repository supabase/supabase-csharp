using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Channels;

/// <summary>
///     A channel's join lifecycle over an unconnected socket: pushing before a join is refused, a join
///     that gets no reply fails <c>Subscribe</c>, and unsubscribing walks state through <c>Leaving</c> to
///     <c>Closed</c>. The join handshake itself needs a server reply and is covered in the E2E tier.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ChannelLifecycleTests
{
    [TestMethod]
    public void Push_ShouldThrow_GivenNotJoined()
    {
        var channel = Wire.Channel();
        var act = () => channel.Push("some_event");
        act.Should().Throw<RealtimeException>().Which.Reason.Should().Be(FailureHint.Reason.ChannelNotOpen);
    }

    [TestMethod]
    public async Task Subscribe_ShouldThrowPushTimeout_GivenNoJoinReply()
    {
        var channel = Wire.Channel();
        var act = () => channel.Subscribe(200).WaitAsync(TimeSpan.FromSeconds(5));
        (await act.Should().ThrowAsync<RealtimeException>()).Which.Reason.Should().Be(FailureHint.Reason.PushTimeout,
            "a join the server never answers must fail Subscribe instead of hanging (issue #479)");
    }

    [TestMethod]
    public void Unsubscribe_ShouldWalkStateToClosed()
    {
        var channel = Wire.Channel();
        var states = new List<ChannelState>();
        channel.AddStateChangedHandler((_, state) => states.Add(state));
        channel.Unsubscribe();
        states.Should().ContainInOrder(ChannelState.Leaving, ChannelState.Closed);
        channel.IsClosed.Should().BeTrue();
    }

    [TestMethod]
    public async Task Unsubscribe_ShouldFailSubscribe_GivenAChannelStillJoining()
    {
        var channel = Wire.Channel();
        var subscribing = channel.Subscribe();
        channel.Unsubscribe();
        var act = () => subscribing.WaitAsync(TimeSpan.FromSeconds(5));
        await act.Should().ThrowAsync<RealtimeException>()
            .WithMessage("Channel closed before it joined")
            .Where(e => e.Reason == FailureHint.Reason.ChannelJoinFailure, "leaving during the join must fail Subscribe instead of hanging");
    }

    [TestMethod]
    public void RejoinTimer_ShouldTickEveryTimeoutUntilClosed_GivenACustomTimeProvider()
    {
        var clock = new FakeTimeProvider();
        var rejoinTicks = 0;
        var timeout = TimeSpan.FromSeconds(10);
        var options = new ClientOptions { Timeout = timeout, TimeProvider = ObservingRejoinTicks(clock, () => rejoinTicks++) };
        var channel = Wire.Channel(options: ChannelOptions.Public(options, () => null, Wire.Settings()));
        channel.Rejoin(timeoutMs: 600_000);
        clock.Advance(timeout - TimeSpan.FromTicks(1));
        rejoinTicks.Should().Be(0, "a joining channel's rejoin timer waits a full Timeout on the supplied clock");
        clock.Advance(TimeSpan.FromTicks(1));
        rejoinTicks.Should().Be(1, "the rejoin timer fires once Timeout has elapsed on the supplied clock");
        clock.Advance(timeout);
        rejoinTicks.Should().Be(2, "the rejoin timer repeats every Timeout");
        channel.Unsubscribe();
        clock.Advance(timeout * 3);
        rejoinTicks.Should().Be(2, "closing the channel stops the rejoin timer");
    }

    [TestMethod]
    public void LeavePush_ShouldCarryTheJoinRef_GivenASentJoin()
    {
        var channel = Wire.Channel();
        channel.JoinPush = channel.GenerateJoinPush();
        channel.JoinPush.Send();
        var leave = new Push(channel.Socket, channel, ChannelEventLeave);
        leave.Send();
        leave.Message!.JoinRef.Should().Be(channel.JoinPush.Ref, "Realtime ignores a phx_leave without the join ref (issue #125)");
    }

    private static TimeProvider ObservingRejoinTicks(FakeTimeProvider clock, Action onRejoinTick)
    {
        var provider = Substitute.For<TimeProvider>();
        provider.CreateTimer(default!, default, default, default).ReturnsForAnyArgs(call =>
        {
            var callback = call.ArgAt<TimerCallback>(0);
            return clock.CreateTimer(callback.Target is RealtimeChannel ? Observed(callback) : callback,
                call.ArgAt<object?>(1), call.ArgAt<TimeSpan>(2), call.ArgAt<TimeSpan>(3));
        });
        return provider;

        TimerCallback Observed(TimerCallback callback) => state =>
        {
            onRejoinTick();
            callback(state);
        };
    }
}
