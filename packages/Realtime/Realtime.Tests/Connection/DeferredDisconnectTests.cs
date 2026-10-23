using System;
using System.Threading.Tasks;
using FluentAssertions;
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

    private static async Task<Client> Connect(TimeSpan disconnectAfter)
    {
        var options = new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(new FakeWebsocketClient { OpenOnStart = true }),
            DisconnectOnEmptyChannelsAfter = disconnectAfter,
        };
        var client = new Client("ws://localhost/socket", options);
        await client.ConnectAsync();
        return client;
    }
}
