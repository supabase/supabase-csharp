using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Channels;

/// <summary>
///     A channel that rejoins after its socket came back sends what the app queued while it was away.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ChannelRejoinTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, AnswerJoins = true };
    private Client client = null!;

    [TestInitialize]
    public async Task Connect()
    {
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await this.client.ConnectAsync();
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public async Task Send_ShouldGoOutAfterTheRejoin_GivenTheSocketWasDown()
    {
        var sent = this.socket.SentFrame("broadcast", 1);
        await this.SendWhileTheSocketIsDown();
        this.socket.RecoverFromLostConnection();
        await sent.Awaiting(frame => frame).Should().CompleteWithinAsync(Patience, "a message queued while the socket was down must go out once the channel has rejoined");
    }

    [TestMethod]
    public async Task Send_ShouldGoOutOnce_GivenTheChannelRejoinsAgain()
    {
        var first = this.socket.SentFrame("broadcast", 1);
        var second = this.socket.SentFrame("broadcast", 2);
        var secondRejoin = this.socket.SentFrame("phx_join", 3);
        await this.SendWhileTheSocketIsDown();
        this.socket.RecoverFromLostConnection();
        await first.WaitAsync(Patience);
        this.socket.RecoverFromLostConnection();
        await secondRejoin.WaitAsync(Patience);
        await second.Awaiting(frame => frame).Should().NotCompleteWithinAsync(Quiet, "a queued message must go out on the first rejoin only");
    }

    private async Task SendWhileTheSocketIsDown()
    {
        var channel = this.client.Channel("room");
        await channel.Subscribe().WaitAsync(Patience);
        this.socket.IsRunning = false;
        await channel.Send(ChannelEventName.Broadcast, "hello", new { });
        this.socket.IsRunning = true;
    }
}
