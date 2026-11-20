using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Models;
using Supabase.Realtime.Socket;

namespace Realtime.Tests.Socket;

/// <summary>
///     A client set to <see cref="ProtocolVersion.V2" /> speaks protocol 2.0.0: JSON arrays in text frames, and a
///     broadcast of bytes in a binary frame (realtime.configuration.binary_protocol). Runs over a fake socket, so
///     no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ProtocolVersionTests
{
    private const string Topic = "realtime:room";
    private const string Event = "frame";
    private const string JoinRef = "1";
    private const string Ref = "22";
    private const char TwoByteCharacter = (char) 0xE9;
    private const byte UserBroadcastPush = 3;
    private const byte UserBroadcast = 4;
    private const byte NoMetadata = 0;
    private const byte BinaryEncoding = 0;
    private const byte JsonEncoding = 1;
    private const byte UnknownEncoding = 2;
    private const int LongestFieldInBytes = 255;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly byte[] Bytes = [0, 159, 146, 150, 255];

    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, AnswerJoins = true };
    private readonly ClientOptions options = new() { ProtocolVersion = ProtocolVersion.V2 };
    private FakeWebSocketFactory factory = null!;
    private Client client = null!;

    [TestInitialize]
    public void CreateClient()
    {
        this.factory = new FakeWebSocketFactory(this.socket);
        this.options.WebSocketFactory = this.factory;
        this.client = new Client("ws://localhost/socket", this.options);
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    [DataRow(ProtocolVersion.V1, "vsn=1.0.0")]
    [DataRow(ProtocolVersion.V2, "vsn=2.0.0")]
    public async Task ConnectAsync_ShouldAskForTheProtocolVersion_GivenTheOption(ProtocolVersion version, string parameter)
    {
        this.options.ProtocolVersion = version;
        await this.client.ConnectAsync();
        this.factory.CreatedUri!.Query.Should().Contain(parameter, "the server picks the protocol from the vsn parameter of the socket URL");
    }

    [TestMethod]
    [DataRow(ProtocolVersion.V1, "1", """{"topic":"realtime:room","event":"phx_leave","payload":{},"ref":"2","join_ref":"1"}""")]
    [DataRow(ProtocolVersion.V2, "1", """["1","2","realtime:room","phx_leave",{}]""")]
    [DataRow(ProtocolVersion.V2, null, """[null,"2","realtime:room","phx_leave",{}]""")]
    public async Task Push_ShouldWriteAnObjectOrAnArray_GivenTheProtocolVersion(ProtocolVersion version, string? joinRef, string frame)
    {
        this.options.ProtocolVersion = version;
        var sent = this.socket.SentText("phx_leave", 1);
        await this.client.ConnectAsync();
        this.client.Socket!.Push(new SocketRequest { Topic = Topic, Event = "phx_leave", Payload = new { }, Ref = "2", JoinRef = joinRef });
        (await sent.WaitAsync(Patience)).Should().Be(frame, "protocol 1.0.0 writes a JSON object, and 2.0.0 must write the array of join_ref, ref, topic, event and payload");
    }

    [TestMethod]
    public async Task Subscribe_ShouldJoin_GivenTheServerRepliesWithAnArray()
    {
        await this.client.ConnectAsync();
        var channel = this.client.Channel("room");
        await channel.Awaiting(c => c.Subscribe()).Should().CompleteWithinAsync(Patience, "a protocol 2.0.0 reply is an array, and the client must read it as the reply to its join");
    }

    [TestMethod]
    public async Task Broadcast_ShouldBeDelivered_GivenABinaryFrameWithAJsonPayload()
    {
        var broadcast = await this.JoinedBroadcast();
        this.socket.ReceiveBinary(BinaryFrame(UserBroadcast, """{"id":"7"}""", JsonEncoding, Utf8("""{"x":1}""")));
        JsonSerializer.Serialize(broadcast.Current()).Should().Be("""{"event":"frame","payload":{"x":1},"meta":{"id":"7","replayed":false}}""", "a user broadcast frame must be delivered with its event, its JSON payload and its metadata");
    }

    [TestMethod]
    [DataRow(BinaryEncoding)]
    [DataRow(UnknownEncoding)]
    public async Task Broadcast_ShouldCarryTheBytes_GivenABinaryFrameWhosePayloadIsNotJson(byte encoding)
    {
        var broadcast = await this.JoinedBroadcast();
        this.socket.ReceiveBinary(BinaryFrame(UserBroadcast, string.Empty, encoding, Bytes));
        broadcast.Current()!.BinaryPayload.Should().Equal(Bytes, "a payload that is not JSON must reach the app as the bytes the server sent");
    }

    [TestMethod]
    public async Task BinaryFrame_ShouldBeReported_GivenItIsNotAUserBroadcast()
    {
        string? reported = null;
        this.client.AddDebugHandler((sender, _, exception) => reported = ReferenceEquals(sender, this.client.Socket) && exception is NotSupportedException ? exception.Message : reported);
        await this.JoinedBroadcast();
        this.socket.ReceiveBinary(BinaryFrame(UserBroadcastPush, string.Empty, BinaryEncoding, Bytes));
        reported.Should().Be("Binary frame type 3 is not a user broadcast.", "only a user broadcast frame carries a broadcast, so any other binary frame must be reported and not read as one");
    }

    [TestMethod]
    public async Task Push_ShouldWriteTheUserBroadcastPushFrame_GivenABinaryBroadcast()
    {
        await this.client.ConnectAsync();
        this.client.Socket!.Push(new SocketRequest { Topic = Topic, Event = "broadcast", Payload = new BinaryBroadcast(Event, Bytes), Ref = Ref, JoinRef = JoinRef });
        (await this.socket.SentBinary.Task.WaitAsync(Patience)).Should().Equal([UserBroadcastPush, (byte) JoinRef.Length, (byte) Ref.Length, (byte) Topic.Length, (byte) Event.Length, NoMetadata, BinaryEncoding, .. Utf8(JoinRef + Ref + Topic + Event), .. Bytes], "the push frame must hold its type, the sizes of join_ref, ref, topic and event, no metadata, the binary encoding, then those fields and the payload");
    }

    [TestMethod]
    public async Task SendBinary_ShouldPushTheBytes_GivenAnEventNameOfTheLongestSize()
    {
        var longestEvent = new string('e', LongestFieldInBytes);
        var broadcast = await this.JoinedBroadcast();
        await broadcast.SendBinary(longestEvent, Bytes);
        (await this.socket.SentBinary.Task.WaitAsync(Patience)).Should().EndWith([.. Utf8(Topic + longestEvent), .. Bytes], "the bytes must go out in a binary frame after the topic and an event name of up to 255 bytes");
    }

    [TestMethod]
    public async Task SendBinary_ShouldPushTheBytesItWasGiven_GivenTheArrayChangesAfterTheCall()
    {
        byte[] reused = [.. Bytes];
        var broadcast = await this.JoinedBroadcast();
        await broadcast.SendBinary(Event, reused);
        Array.Clear(reused);
        (await this.socket.SentBinary.Task.WaitAsync(Patience)).Should().EndWith(Bytes, "the frame is written after SendBinary returns, so it must carry the bytes as they were at the call");
    }

    [TestMethod]
    public async Task SendBinary_ShouldThrow_GivenProtocolV1()
    {
        this.options.ProtocolVersion = ProtocolVersion.V1;
        var broadcast = await this.JoinedBroadcast();
        (await broadcast.Awaiting(b => b.SendBinary(Event, Bytes)).Should().ThrowAsync<RealtimeException>()).WithMessage("A broadcast of bytes needs ClientOptions.ProtocolVersion set to V2.", "protocol 1.0.0 has no binary frame, so the bytes must not be sent");
    }

    [TestMethod]
    public async Task SendBinary_ShouldThrow_GivenAnEventNameOverTheLongestSizeInBytes()
    {
        var overTheSizeInBytesOnly = new string(TwoByteCharacter, (LongestFieldInBytes + 1) / 2);
        var broadcast = await this.JoinedBroadcast();
        (await broadcast.Awaiting(b => b.SendBinary(overTheSizeInBytesOnly, Bytes)).Should().ThrowAsync<ArgumentException>()).WithMessage("The event name of a broadcast of bytes must fit in 255 bytes.*", "a binary frame has one byte for the size of the event name in bytes, so a longer one must be refused");
    }

    [TestMethod]
    public async Task SendBinary_ShouldThrow_GivenNoPayload()
    {
        var broadcast = await this.JoinedBroadcast();
        await broadcast.Awaiting(b => b.SendBinary(Event, null!)).Should().ThrowAsync<ArgumentNullException>("a broadcast of bytes without bytes must be refused before anything is sent");
    }

    [TestMethod]
    public async Task SendBinary_ShouldThrow_GivenATopicOverTheLongestSize()
    {
        var broadcast = await this.JoinedBroadcast(new string('t', LongestFieldInBytes));
        (await broadcast.Awaiting(b => b.SendBinary(Event, Bytes)).Should().ThrowAsync<RealtimeException>()).WithMessage("The topic of a broadcast of bytes must fit in 255 bytes.", "a binary frame has one byte for the size of the topic, so a longer one must be refused");
    }

    [TestMethod]
    public async Task SendBinary_ShouldThrow_GivenTheServerRejectsTheBroadcast()
    {
        var broadcast = await this.JoinedBroadcast(acknowledged: true);
        var send = broadcast.SendBinary(Event, Bytes);
        this.socket.Reply(Topic, this.client.Channel(Topic).LastPush!.Ref, "error");
        (await send.Awaiting(s => s).Should().ThrowWithinAsync<RealtimeException>(Patience)).WithMessage("The server rejected the broadcast of bytes.", "with ack on, an error reply means the server did not accept the broadcast, so the caller must be told");
    }

    [TestMethod]
    public async Task SendBinary_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        var broadcast = await this.JoinedBroadcast(acknowledged: true);
        BlockingCaller.Completes(() =>
        {
            var sending = broadcast.SendBinary(Event, Bytes);
            Task.Run(() => this.socket.Reply(Topic, this.client.Channel(Topic).LastPush!.Ref));
            return sending;
        }).Should().BeTrue("the SDK must not resume on the caller's synchronization context");
    }

    private async Task<RealtimeBroadcast<BaseBroadcast>> JoinedBroadcast(string channelName = "room", bool acknowledged = false)
    {
        var firstHeartbeat = this.socket.SentText("heartbeat", 1);
        await this.client.ConnectAsync();
        await firstHeartbeat.WaitAsync(Patience);
        var channel = this.client.Channel(channelName);
        var broadcast = channel.Register<BaseBroadcast>(broadcastAck: acknowledged);
        await channel.Subscribe().WaitAsync(Patience);
        return broadcast;
    }

    private static byte[] BinaryFrame(byte type, string metadata, byte encoding, byte[] payload) =>
        [type, (byte) Topic.Length, (byte) Event.Length, (byte) metadata.Length, encoding, .. Utf8(Topic + Event + metadata), .. payload];

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
