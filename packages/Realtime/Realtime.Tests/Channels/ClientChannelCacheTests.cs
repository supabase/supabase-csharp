using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Channels;

/// <summary>
///     The client hands out a fresh channel after the cached one is unsubscribed, keeps its socket when
///     connected again, and drops every channel and the socket on Disconnect or RemoveAll. Runs over a fake socket that
///     reports itself connected, so no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ClientChannelCacheTests
{
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true };
    private Client client = null!;

    [TestInitialize]
    public async Task Connect()
    {
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await this.client.ConnectAsync();
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldKeepTheSocket_GivenAlreadyConnected()
    {
        var channel = this.client.Channel("room");
        await this.client.ConnectAsync();
        channel.Socket.Should().BeSameAs(this.client.Socket, "a second ConnectAsync must not replace the open socket");
    }

    [TestMethod]
    public void Channel_ShouldReturnAFreshChannel_GivenUnsubscribe()
    {
        var named = this.client.Channel("room");
        var table = this.client.Channel("realtime", "public", "todos");
        named.Unsubscribe();
        table.Unsubscribe();
        this.client.Channel("room").Should().NotBeSameAs(named);
        this.client.Channel("realtime", "public", "todos").Should().NotBeSameAs(table);
    }

    [TestMethod]
    public void Unsubscribe_ShouldKeepTheNewerChannel_GivenAStaleChannelClosesAgain()
    {
        var first = this.client.Channel("room");
        first.Unsubscribe();
        var second = this.client.Channel("room");
        first.Unsubscribe();
        this.client.Channel("room").Should().BeSameAs(second);
    }

    [TestMethod]
    public async Task Subscribe_ShouldKeepTheChannel_GivenItIsJoining()
    {
        var channel = this.client.Channel("room");
        _ = channel.Subscribe(100);
        await Task.Yield();
        this.client.Channel("room").Should().BeSameAs(channel);
    }

    [TestMethod]
    public async Task Subscribe_ShouldAddTheChannelBack_GivenTheSameInstanceAfterUnsubscribe()
    {
        var channel = this.client.Channel("room");
        channel.Unsubscribe();
        _ = channel.Subscribe(100);
        await Task.Yield();
        this.client.Channel("room").Should().BeSameAs(channel);
    }

    [TestMethod]
    public void RemoveAll_ShouldRemoveEveryChannel()
    {
        this.client.Channel("room");
        this.client.Channel("lobby");
        this.client.RemoveAll();
        this.client.Subscriptions.Should().BeEmpty();
    }

    [TestMethod]
    public void RemoveAll_ShouldDisconnect()
    {
        this.client.RemoveAll();
        this.client.Socket.Should().BeNull();
    }

    [TestMethod]
    public async Task Channel_ShouldUseTheNewSocket_GivenAChannelResubscribesWhileDisconnecting()
    {
        var channel = this.client.Channel("room");
        channel.AddStateChangedHandler((_, state) =>
        {
            if (state == ChannelState.Closed)
                channel.Subscribe(100);
        });
        this.client.Disconnect();
        await this.client.ConnectAsync();
        this.client.Channel("room").Socket.Should().BeSameAs(this.client.Socket);
    }

    [TestMethod]
    public void Disconnect_ShouldUnsubscribe_GivenAJoinedChannel()
    {
        var channel = this.client.Channel("room");
        _ = channel.Subscribe();
        this.socket.Reply(channel.Topic, channel.JoinPush!.Ref);
        channel.IsSubscribed.Should().BeTrue();
        this.client.Disconnect();
        channel.IsSubscribed.Should().BeFalse();
    }

    [TestMethod]
    public void Disconnect_ShouldFailSubscribe_GivenAChannelStillJoining()
    {
        var subscribing = this.client.Channel("room").Subscribe();
        this.client.Disconnect();
        var exception = subscribing.Exception!.InnerException.Should().BeOfType<RealtimeException>().Subject;
        exception.Reason.Should().Be(FailureHint.Reason.ChannelJoinFailure);
        exception.Message.Should().Be("Channel closed before it joined");
    }
}
