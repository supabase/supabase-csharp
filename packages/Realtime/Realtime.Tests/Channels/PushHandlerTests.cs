using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Channels;

/// <summary>
///     A push tells its message handlers about the reply, and a removed handler is no longer told.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PushHandlerTests
{
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true };
    private Client client = null!;
    private RealtimeChannel channel = null!;
    private const string PresenceDiff = """{"topic":"realtime:room","event":"presence_diff","ref":null,"payload":{"joins":{},"leaves":{}}}""";
    private int calls;

    [TestInitialize]
    public async Task Connect()
    {
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await this.client.ConnectAsync();
        await this.socket.FirstSend.Task;
        this.channel = this.client.Channel("room");
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public void Push_ShouldCallTheHandlerOnce_GivenAReply()
    {
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken);
        push.AddMessageReceivedHandler((_, _) => this.calls++);
        push.Send();

        this.socket.Reply(this.channel.Topic, push.Ref);

        this.calls.Should().Be(1, "the handler is told about the reply to the push");
    }

    [TestMethod]
    public void Push_ShouldWaitForItsReply_GivenAPresenceDiff()
    {
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken);
        push.AddMessageReceivedHandler((_, _) => this.calls++);
        push.Send();
        this.socket.Receive(PresenceDiff);
        this.calls.Should().Be(0, "a presence message must not be taken for the reply to a push");
        this.socket.Reply(this.channel.Topic, push.Ref);
        this.calls.Should().Be(1, "the push still hears its own reply");
    }

    [TestMethod]
    public void Push_ShouldNotCallTheHandler_GivenAPresenceDiffBeforeItIsSent()
    {
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken);
        push.AddMessageReceivedHandler((_, _) => this.calls++);
        this.socket.Receive(PresenceDiff);
        this.calls.Should().Be(0, "a push that was not sent yet has no reply to hear");
    }

    [TestMethod]
    public void Push_ShouldNotCallTheHandler_GivenItWasRemoved()
    {
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken);
        IRealtimePush<RealtimeChannel, SocketResponse>.MessageEventHandler handler = (_, _) => this.calls++;
        push.AddMessageReceivedHandler(handler);
        push.RemoveMessageReceivedHandler(handler);
        push.Send();

        this.socket.Reply(this.channel.Topic, push.Ref);

        this.calls.Should().Be(0, "a removed handler must not be told about the reply");
    }

    [TestMethod]
    public void JoinPush_ShouldLeaveTheChannelClosed_GivenAReplyAfterUnsubscribe()
    {
        _ = this.channel.Subscribe();
        this.channel.Unsubscribe();
        this.socket.Reply(this.channel.Topic, this.channel.JoinPush!.Ref);
        this.channel.IsClosed.Should().BeTrue("the server still answers a join sent before the leave");
    }
}
