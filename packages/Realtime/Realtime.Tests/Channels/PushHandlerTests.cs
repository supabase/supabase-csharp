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
    private RealtimeChannel channel = null!;

    [TestInitialize]
    public async Task Connect()
    {
        var client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await client.ConnectAsync();
        this.channel = client.Channel("room");
    }

    [TestMethod]
    public void Push_ShouldCallTheHandlerOnce_GivenAReply()
    {
        var calls = 0;
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken);
        push.AddMessageReceivedHandler((_, _) => calls++);
        push.Send();
        this.Reply(push.Ref);
        calls.Should().Be(1, "the handler is told about the reply to the push");
    }

    [TestMethod]
    public void Push_ShouldNotCallTheHandler_GivenItWasRemoved()
    {
        var calls = 0;
        var push = new Push(this.channel.Socket, this.channel, ChannelAccessToken);
        IRealtimePush<RealtimeChannel, SocketResponse>.MessageEventHandler handler = (_, _) => calls++;
        push.AddMessageReceivedHandler(handler);
        push.RemoveMessageReceivedHandler(handler);
        push.Send();
        this.Reply(push.Ref);
        calls.Should().Be(0, "a removed handler must not be told about the reply");
    }

    private void Reply(string? reference) =>
        this.socket.Receive("""{"topic":"realtime:room","event":"phx_reply","ref":"REF","payload":{"status":"ok","response":{}}}""".Replace("REF", reference));
}
