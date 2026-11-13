using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Connection;

/// <summary>
///     With <see cref="ClientOptions.DisconnectOnEmptyChannelsAfter" /> set, the client drops the socket once
///     its last channel has been closed for that long. Runs over a fake socket, so no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeferredDisconnectTests
{
    [TestMethod]
    public async Task Unsubscribe_ShouldDisconnect_GivenTheLastChannelClosed()
    {
        var client = await Connect(TimeSpan.FromMilliseconds(50));
        client.Channel("room").Unsubscribe();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (client.Socket != null && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        client.Socket.Should().BeNull();
    }

    [TestMethod]
    public async Task Unsubscribe_ShouldStayConnected_GivenANewChannelBeforeTheDelay()
    {
        var client = await Connect(TimeSpan.FromMilliseconds(200));
        client.Channel("room").Unsubscribe();
        client.Channel("lobby");
        await Task.Delay(400);
        client.Socket.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Unsubscribe_ShouldRestartTheDelay_GivenTheLastChannelClosedAgain()
    {
        var client = await Connect(TimeSpan.FromMilliseconds(300));
        client.Channel("room").Unsubscribe();
        await Task.Delay(150);
        client.Channel("lobby").Unsubscribe();
        await Task.Delay(200);
        client.Socket.Should().NotBeNull("the delay counts from the last channel that closed");
    }

    [TestMethod]
    public async Task Unsubscribe_ShouldDisconnectAfterTheDelay_GivenACustomTimeProvider()
    {
        var clock = new FakeTimeProvider();
        var delay = TimeSpan.FromMinutes(5);
        var client = await Connect(delay, clock);
        client.Channel("room").Unsubscribe();
        clock.Advance(delay - TimeSpan.FromTicks(1));
        await Task.Delay(100);
        client.Socket.Should().NotBeNull("the delay has not yet elapsed on the supplied clock");
        clock.Advance(TimeSpan.FromTicks(1));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (client.Socket != null && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        client.Socket.Should().BeNull("the deferred disconnect waits on the supplied clock");
    }

    [TestMethod]
    public async Task Unsubscribe_ShouldDisconnectWithoutTheCallerContext_GivenTheDelayElapses()
    {
        var clock = new FakeTimeProvider();
        var delay = TimeSpan.FromMinutes(5);
        var client = await Connect(delay, clock);

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new NeverRunsContinuations());
        try
        {
            client.Channel("room").Unsubscribe();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        // Completing off the captured context forces a Post; completing on it would let the continuation run inline.
        clock.Advance(delay);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (client.Socket != null && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        client.Socket.Should().BeNull("the deferred disconnect must not wait on the caller's context, which may be blocked");
    }

    [TestMethod]
    public void ClientOptions_ShouldDefaultToSystemTimeProvider() =>
        new ClientOptions().TimeProvider.Should().BeSameAs(TimeProvider.System, "unset, every Realtime timer keeps running on the real clock");

    private static async Task<Client> Connect(TimeSpan disconnectAfter, TimeProvider? clock = null)
    {
        var options = new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(new FakeWebsocketClient { OpenOnStart = true }),
            DisconnectOnEmptyChannelsAfter = disconnectAfter,
            TimeProvider = clock ?? TimeProvider.System,
        };
        var client = new Client("ws://localhost/socket", options);
        await client.ConnectAsync();
        return client;
    }
}
