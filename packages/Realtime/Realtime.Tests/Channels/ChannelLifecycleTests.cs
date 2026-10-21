using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
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
    public void LeavePush_ShouldCarryTheJoinRef_GivenASentJoin()
    {
        var channel = Wire.Channel();
        channel.JoinPush = channel.GenerateJoinPush();
        channel.JoinPush.Send();
        var leave = new Push(channel.Socket, channel, ChannelEventLeave);
        leave.Send();
        leave.Message!.JoinRef.Should().Be(channel.JoinPush.Ref, "Realtime ignores a phx_leave without the join ref (issue #125)");
    }
}
