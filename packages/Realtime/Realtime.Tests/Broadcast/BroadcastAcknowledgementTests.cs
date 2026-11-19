using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Broadcast;

/// <summary>
///     A broadcast sent on a channel with <c>ack</c> on resolves with whether the server accepted it.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class BroadcastAcknowledgementTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, AnswerJoins = true };
    private Client client = null!;
    private RealtimeChannel channel = null!;

    [TestInitialize]
    public async Task Join()
    {
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await this.client.ConnectAsync();
        this.channel = this.client.Channel("room");
        this.channel.Register<BroadcastExample>(broadcastAck: true);
        await this.channel.Subscribe().WaitAsync(Patience);
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    [DataRow("ok", true)]
    [DataRow("error", false)]
    public async Task Send_ShouldResolveWithWhetherTheServerAcceptedIt_GivenBroadcastAck(string status, bool accepted)
    {
        var send = this.channel.Send(ChannelEventName.Broadcast, "hello", new { });
        this.socket.Reply(this.channel.Topic, this.channel.LastPush!.Ref, status);
        (await send.WaitAsync(Patience)).Should().Be(accepted, "an acknowledged send must report the status of the server's reply");
    }
}
