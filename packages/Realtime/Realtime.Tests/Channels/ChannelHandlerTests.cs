using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;

namespace Realtime.Tests.Channels;

/// <summary>
///     Handlers registered on a channel (state, message, error, broadcast and presence) are called while
///     registered and are not called once removed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ChannelHandlerTests
{
    private const string BroadcastFrame =
        "{\"topic\":\"realtime:example\",\"event\":\"broadcast\",\"ref\":\"srv-1\",\"payload\":{\"event\":\"user\",\"userId\":\"abc-123\"}}";

    private const string PresenceStateFrame =
        "{\"topic\":\"realtime:example\",\"event\":\"presence_state\",\"ref\":\"s1\"," +
        "\"payload\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\",\"time\":\"2023-09-01T10:00:00Z\"}]}}}";

    [TestMethod]
    public void StateChangedHandler_ShouldStopCalling_GivenRemoval()
    {
        var channel = Wire.Channel();
        var calls = 0;
        IRealtimeChannel.StateChangedHandler handler = (_, _) => calls++;
        channel.AddStateChangedHandler(handler);
        channel.Unsubscribe();
        var afterAdd = calls;

        channel.RemoveStateChangedHandler(handler);
        channel.Unsubscribe();

        afterAdd.Should().BeGreaterThan(0);
        calls.Should().Be(afterAdd);
    }

    [TestMethod]
    public void MessageReceivedHandler_ShouldStopCalling_GivenRemoval()
    {
        var channel = Wire.Channel();
        var calls = 0;
        IRealtimeChannel.MessageReceivedHandler handler = (_, _) => calls++;
        channel.AddMessageReceivedHandler(handler);
        channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));
        calls.Should().Be(1);

        channel.RemoveMessageReceivedHandler(handler);
        channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));
        calls.Should().Be(1);
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldStopCalling_GivenRemoval()
    {
        var channel = Wire.Channel();
        var calls = 0;
        IRealtimeChannel.ErrorEventHandler handler = (_, _) => calls++;
        channel.AddErrorHandler(handler);
        await FailJoin(channel);
        var afterAdd = calls;

        channel.RemoveErrorHandler(handler);
        await FailJoin(channel);

        afterAdd.Should().BeGreaterThan(0);
        calls.Should().Be(afterAdd);
    }

    [TestMethod]
    public void BroadcastHandler_ShouldStopCalling_GivenRemoval()
    {
        var channel = Wire.Channel();
        var broadcast = channel.Register<BroadcastExample>();
        var calls = 0;
        IRealtimeBroadcast.BroadcastEventHandler handler = (_, _) => calls++;
        broadcast.AddBroadcastEventHandler(handler);
        channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));
        calls.Should().Be(1);

        broadcast.RemoveBroadcastEventHandler(handler);
        channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));
        calls.Should().Be(1);
    }

    [TestMethod]
    public void PresenceHandler_ShouldStopCalling_GivenRemoval()
    {
        var channel = Wire.Channel();
        var presence = channel.Register<PresenceExample>("user-1");
        var calls = 0;
        IRealtimePresence.PresenceEventHandler handler = (_, _) => calls++;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, handler);
        channel.HandleSocketMessage(Wire.Decode(PresenceStateFrame));
        calls.Should().Be(1);

        presence.RemovePresenceEventHandlers(IRealtimePresence.EventType.Sync, handler);
        channel.HandleSocketMessage(Wire.Decode(PresenceStateFrame));
        calls.Should().Be(1);
    }

    private static async Task FailJoin(RealtimeChannel channel)
    {
        var act = () => channel.Subscribe(100).WaitAsync(TimeSpan.FromSeconds(5));
        await act.Should().ThrowAsync<RealtimeException>();
        // Error handlers run right after the state change that fails Subscribe; let them finish.
        await Task.Delay(200);
    }
}
