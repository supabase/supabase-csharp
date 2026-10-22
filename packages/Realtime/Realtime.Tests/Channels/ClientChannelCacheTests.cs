using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Channels;

/// <summary>
///     The client hands out a fresh channel after the cached one is unsubscribed. Runs over a fake socket that
///     reports itself connected, so no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ClientChannelCacheTests
{
    private Client client = null!;

    [TestInitialize]
    public async Task Connect()
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true };
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket) });
        await this.client.ConnectAsync();
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
}
