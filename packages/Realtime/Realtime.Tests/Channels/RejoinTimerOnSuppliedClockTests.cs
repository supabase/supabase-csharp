using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Channel;

namespace Realtime.Tests.Channels;

/// <summary>
///     A channel keeps one repeating rejoin timer on <see cref="ClientOptions.TimeProvider" />: it starts when the
///     channel starts joining, keeps its countdown when another join comes while it runs, and stops when the channel closes.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class RejoinTimerOnSuppliedClockTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ITimer timer = Substitute.For<ITimer>();
    private readonly RealtimeChannel channel;

    public RejoinTimerOnSuppliedClockTests()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.CreateTimer(default!, default, default, default).ReturnsForAnyArgs(this.timer);
        var options = new ClientOptions { Timeout = Timeout, TimeProvider = clock };
        this.channel = Wire.Channel(options: ChannelOptions.Public(options, () => null, Wire.Settings()));
    }

    [TestMethod]
    public void RejoinTimer_ShouldRepeatEveryTimeout_GivenTheChannelStartedJoining()
    {
        this.channel.Rejoin();
        this.timer.Received(1).Change(Timeout, Timeout);
    }

    [TestMethod]
    public void RejoinTimer_ShouldKeepItsCountdown_GivenAnotherJoinWhileItRuns()
    {
        this.channel.Rejoin();
        this.channel.Rejoin();
        this.timer.Received(1).Change(Timeout, Timeout);
    }

    [TestMethod]
    public void RejoinTimer_ShouldStart_GivenAJoinAfterTheChannelClosed()
    {
        this.channel.Rejoin();
        this.channel.Unsubscribe();
        this.channel.Rejoin();
        this.timer.Received(2).Change(Timeout, Timeout);
    }

    [TestMethod]
    public void RejoinTimer_ShouldStop_GivenTheChannelClosed()
    {
        this.channel.Rejoin();
        this.channel.Unsubscribe();
        this.timer.Received().Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
    }
}
