using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Connection;

/// <summary>
///     The wait <see cref="ClientOptions.ReconnectAfterInterval" /> asks for runs on <see cref="ClientOptions.TimeProvider" />.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ReconnectOnSuppliedClockTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromHours(1);

    private readonly FakeTimeProvider clock = new();
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, ErrorReconnectTimeout = TimeSpan.FromSeconds(1) };
    private Client client = null!;

    [TestInitialize]
    public async Task Connect()
    {
        this.client = new Client("ws://localhost/socket",
            new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket), ReconnectAfterInterval = _ => Delay, TimeProvider = this.clock });
        await this.client.ConnectAsync();
        this.socket.IsRunning = false;
        this.socket.Fail(new InvalidOperationException("refused"));
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public void ReconnectAfterInterval_ShouldReconnect_GivenTheDelayPassedOnTheSuppliedClock()
    {
        this.clock.Advance(Delay);
        this.socket.ReconnectCallCount.Should().Be(1, "the delay runs on the supplied clock");
    }

    [TestMethod]
    public void ReconnectAfterInterval_ShouldWait_GivenTheSuppliedClockIsShortOfTheDelay()
    {
        this.clock.Advance(Delay - TimeSpan.FromTicks(1));
        this.socket.ReconnectCallCount.Should().Be(0, "the client waits the whole delay before it tries again");
    }
}
