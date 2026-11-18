using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using Websocket.Client.Exceptions;

namespace Realtime.Tests.Connection;

/// <summary>
///     The client drops the socket once its last channel has been closed for
///     <see cref="ClientOptions.DisconnectOnEmptyChannelsAfter" />, and a channel that subscribes while the client
///     has no socket connects it again. Runs over a fake socket, so no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class DeferredDisconnectTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task Unsubscribe_ShouldDisconnect_GivenTheLastChannelClosed()
    {
        var client = await Connect(TimeSpan.FromMilliseconds(50));
        await DisconnectByItself(client);
        client.Socket.Should().BeNull();
    }

    [TestMethod]
    public async Task Subscribe_ShouldConnectAgain_GivenTheClientDisconnectedByItself()
    {
        var client = await Connect(TimeSpan.FromMilliseconds(50), new FakeWebsocketClient { OpenOnStart = true, AnswerJoins = true });
        var channel = client.Channel("room");
        await DisconnectByItself(client);
        var joining = channel.Subscribe();
        await joining.Awaiting(join => join).Should().CompleteWithinAsync(Patience, "a channel must open a new socket and join on it once the idle one is gone");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldOpenTheSocketItsChannelsHold_GivenAChannelCreatedBeforeIt()
    {
        var client = NotConnected(new FakeWebsocketClient { OpenOnStart = true });
        var channel = client.Channel("room");
        client.Channel("lobby");
        await client.ConnectAsync();
        channel.Socket.Should().BeSameAs(client.Socket, "a channel created before the connect must not be left on a socket that never opens");
    }

    [TestMethod]
    public async Task Subscribe_ShouldFail_GivenTheClientCannotConnect()
    {
        var handshake = new TaskCompletionSource();
        var client = NotConnected(new FakeWebsocketClient { Handshake = handshake });
        _ = client.Channel("room").Subscribe();
        var joining = client.Channel("lobby").Subscribe();
        handshake.SetException(new WebsocketException("refused"));
        (await joining.Awaiting(join => join).Should().ThrowWithinAsync<RealtimeException>(Patience)).Which.Reason.Should().Be(FailureHint.Reason.SocketError, "every channel waiting for the connection must report that it could not open");
    }

    [TestMethod]
    public async Task Subscribe_ShouldFail_GivenUnsubscribeWhileTheClientConnects()
    {
        var handshake = new TaskCompletionSource();
        var channel = NotConnected(new FakeWebsocketClient { Handshake = handshake, AnswerJoins = true }).Channel("room");
        var joining = channel.Subscribe();
        channel.Unsubscribe();
        handshake.SetResult();
        (await joining.Awaiting(join => join).Should().ThrowWithinAsync<RealtimeException>(Patience)).WithMessage("Channel closed before it joined", "a channel must not join after the app unsubscribed it");
    }

    [TestMethod]
    public void Subscribe_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        var handshake = new TaskCompletionSource();
        var client = NotConnected(new FakeWebsocketClient { Handshake = handshake, AnswerJoins = true });
        BlockingCaller.Completes(() =>
        {
            var joining = client.Channel("room").Subscribe();
            Task.Run(handshake.SetResult);
            return joining;
        }).Should().BeTrue("the SDK must not resume on the caller's synchronization context");
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

    private static async Task DisconnectByItself(Client client)
    {
        client.Channel("room").Unsubscribe();
        var deadline = DateTime.UtcNow.Add(Patience);
        while (client.Socket != null && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    private static Client NotConnected(FakeWebsocketClient socket) =>
        new("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket) });

    private static async Task<Client> Connect(TimeSpan disconnectAfter, params FakeWebsocketClient[] next)
    {
        var options = new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory([new FakeWebsocketClient { OpenOnStart = true }, .. next]),
            DisconnectOnEmptyChannelsAfter = disconnectAfter,
        };
        var client = new Client("ws://localhost/socket", options);
        await client.ConnectAsync();
        return client;
    }
}
