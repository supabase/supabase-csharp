using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Connection;

/// <summary>
///     A probe's deadline waits on <see cref="ClientOptions.TimeProvider" />.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ProbeOnSuppliedClockTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromHours(1);

    [TestMethod]
    public async Task ProbeConnection_ShouldReconnect_GivenNoReplyBeforeTheDeadlineOnTheSuppliedClock()
    {
        var clock = new FakeTimeProvider();
        var socket = new FakeWebsocketClient { OpenOnStart = true };
        var client = new Client("ws://localhost/socket",
            new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket), HeartbeatInterval = TimeSpan.FromDays(1), ProbeTimeout = ProbeTimeout, TimeProvider = clock });
        var first = socket.SentFrame("heartbeat", 1);
        await client.ConnectAsync();
        await first.WaitAsync(Patience);
        client.ProbeConnection();
        await clock.AdvanceUntilCompleted(socket.Reconnected.Task, ProbeTimeout, Patience);
        socket.ReconnectCallCount.Should().Be(1, "a probe unanswered at its deadline on the supplied clock is a dead connection");
        client.Disconnect();
    }
}
