using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
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
    private const int JoinTimeoutMs = 50;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly string BroadcastFrame = DataFile.Read("Channels", "BroadcastFrame.json");
    private static readonly string PresenceStateFrame = DataFile.Read("Channels", "PresenceStateFrame.json");

    private readonly RealtimeChannel channel = Wire.Channel();
    private int calls;

    [TestCleanup]
    public void Leave() => this.channel.Unsubscribe();

    [TestMethod]
    public void StateChangedHandler_ShouldBeCalled_GivenAStateChange()
    {
        this.channel.AddStateChangedHandler((_, _) => this.calls++);

        this.channel.Unsubscribe();

        this.calls.Should().BePositive("a registered handler hears the channel leave");
    }

    [TestMethod]
    public void StateChangedHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        IRealtimeChannel.StateChangedHandler handler = (_, _) => this.calls++;
        this.channel.AddStateChangedHandler(handler);
        this.channel.RemoveStateChangedHandler(handler);

        this.channel.Unsubscribe();

        this.calls.Should().Be(0, "a removed handler is not notified");
    }

    [TestMethod]
    public void MessageReceivedHandler_ShouldBeCalled_GivenAFrame()
    {
        this.channel.AddMessageReceivedHandler((_, _) => this.calls++);

        this.channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));

        this.calls.Should().Be(1, "a registered handler gets each inbound frame once");
    }

    [TestMethod]
    public void MessageReceivedHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        IRealtimeChannel.MessageReceivedHandler handler = (_, _) => this.calls++;
        this.channel.AddMessageReceivedHandler(handler);
        this.channel.RemoveMessageReceivedHandler(handler);

        this.channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));

        this.calls.Should().Be(0, "a removed handler is not notified");
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldBeCalled_GivenAJoinTimeout()
    {
        var heard = new TaskCompletionSource();
        this.channel.AddErrorHandler((_, _) => heard.TrySetResult());

        this.StartAJoinThatTimesOut();

        await heard.Awaiting(source => source.Task).Should().CompleteWithinAsync(Patience,
            "a registered handler hears that the join timed out");
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var witnessed = new TaskCompletionSource();
        IRealtimeChannel.ErrorEventHandler handler = (_, _) => this.calls++;
        this.channel.AddErrorHandler(handler);
        this.channel.RemoveErrorHandler(handler);
        this.channel.AddErrorHandler((_, _) => witnessed.TrySetResult());

        this.StartAJoinThatTimesOut();
        await witnessed.Task.WaitAsync(Patience);

        this.calls.Should().Be(0, "a removed handler is not notified of the error its successor heard");
    }

    [TestMethod]
    public void BroadcastHandler_ShouldBeCalled_GivenABroadcast()
    {
        this.channel.Register<BroadcastExample>().AddBroadcastEventHandler((_, _) => this.calls++);

        this.channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));

        this.calls.Should().Be(1, "a registered handler gets each broadcast once");
    }

    [TestMethod]
    public void BroadcastHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var broadcast = this.channel.Register<BroadcastExample>();
        IRealtimeBroadcast.BroadcastEventHandler handler = (_, _) => this.calls++;
        broadcast.AddBroadcastEventHandler(handler);
        broadcast.RemoveBroadcastEventHandler(handler);

        this.channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));

        this.calls.Should().Be(0, "a removed handler is not notified");
    }

    [TestMethod]
    public void PresenceHandler_ShouldBeCalled_GivenAPresenceState()
    {
        this.channel.Register<PresenceExample>("user-1")
            .AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => this.calls++);

        this.channel.HandleSocketMessage(Wire.Decode(PresenceStateFrame));

        this.calls.Should().Be(1, "a registered handler hears each presence sync once");
    }

    [TestMethod]
    public void PresenceHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var presence = this.channel.Register<PresenceExample>("user-1");
        IRealtimePresence.PresenceEventHandler handler = (_, _) => this.calls++;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, handler);
        presence.RemovePresenceEventHandlers(IRealtimePresence.EventType.Sync, handler);

        this.channel.HandleSocketMessage(Wire.Decode(PresenceStateFrame));

        this.calls.Should().Be(0, "a removed handler is not notified");
    }

    [TestMethod]
    public void MessageReceivedHandler_ShouldNotStopTheBroadcast_GivenItThrows()
    {
        this.channel.AddMessageReceivedHandler((_, _) => throw new InvalidOperationException("boom"));
        this.channel.Register<BroadcastExample>().AddBroadcastEventHandler((_, _) => this.calls++);
        this.channel.HandleSocketMessage(Wire.Decode(BroadcastFrame));
        this.calls.Should().Be(1, "a throwing message handler must not stop the rest of the dispatch");
    }

    private void StartAJoinThatTimesOut() => _ = this.channel.Subscribe(JoinTimeoutMs);
}
