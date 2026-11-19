using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Broadcast;

/// <summary>
///     A broadcast sent with <c>ack</c> on still resolves when the server's reply is in before <c>Send</c> has returned.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class BroadcastEarlyReplyTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, AnswerJoins = true, AnswerBroadcasts = true };
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
    public async Task Send_ShouldResolve_GivenTheReplyArrivesBeforeItReturns()
    {
        var send = await Task.Factory.StartNew(() => this.channel.Send(ChannelEventName.Broadcast, "hello", new { }), CancellationToken.None, TaskCreationOptions.None, new InlineTaskScheduler());
        await send.Awaiting(sending => sending).Should().CompleteWithinAsync(Patience, "a reply that arrives before Send listens for it must still complete the send");
    }
}
