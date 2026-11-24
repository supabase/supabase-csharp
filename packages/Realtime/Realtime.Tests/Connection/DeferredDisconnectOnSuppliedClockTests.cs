using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Connection;

/// <summary>
///     The <see cref="ClientOptions.DisconnectOnEmptyChannelsAfter" /> delay waits on <see cref="ClientOptions.TimeProvider" />.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeferredDisconnectOnSuppliedClockTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromHours(1);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider clock = new();
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true };
    private Client client = null!;

    [TestInitialize]
    public async Task Connect()
    {
        this.client = new Client("ws://localhost/socket",
            new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket), DisconnectOnEmptyChannelsAfter = Delay, TimeProvider = this.clock });
        await this.client.ConnectAsync();
        this.client.Channel("room").Unsubscribe();
    }

    [TestMethod]
    public async Task Unsubscribe_ShouldDisconnect_GivenTheDelayPassedOnTheSuppliedClock()
    {
        this.clock.Advance(Delay);
        await this.socket.Awaiting(s => s.Stopped.Task).Should().CompleteWithinAsync(Patience, "the delay runs on the supplied clock");
    }

    [TestMethod]
    public void Unsubscribe_ShouldStayConnected_GivenTheSuppliedClockIsShortOfTheDelay()
    {
        this.clock.Advance(Delay - TimeSpan.FromTicks(1));
        this.socket.Stopped.Task.IsCompleted.Should().BeFalse("the client waits the whole delay before it disconnects");
    }
}
