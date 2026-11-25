using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Models;

namespace Realtime.Tests.Broadcast;

/// <summary>
///     A broadcast sent with a <see cref="BroadcastPersistence" /> asks the server to store it for replay: the
///     option goes out in the metadata of the binary user broadcast frame, and with <c>ack</c> on, the code of an
///     error reply reaches the caller.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class BroadcastPersistenceTests
{
    private const string Topic = "realtime:room";
    private const string Event = "chat";
    private const string StoreForTheMaximumRetention = """{"persist":true}""";
    private const byte UserBroadcastPush = 3;
    private const byte BinaryEncoding = 0;
    private const byte JsonEncoding = 1;
    private const int LongestFieldInBytes = 255;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly byte[] Bytes = [0, 159, 146, 150, 255];

    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, AnswerJoins = true };
    private readonly ClientOptions options = new() { ProtocolVersion = ProtocolVersion.V2 };
    private Client client = null!;
    private RealtimeChannel channel = null!;

    [TestInitialize]
    public void CreateClient()
    {
        this.options.WebSocketFactory = new FakeWebSocketFactory(this.socket);
        this.client = new Client("ws://localhost/socket", this.options);
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    [DataRow(null, StoreForTheMaximumRetention)]
    [DataRow(3600, """{"persist":{"ttl":3600}}""")]
    public async Task SendAsync_ShouldPutThePersistOptionInTheFrameMetadata(int? ttlSeconds, string metadata)
    {
        var broadcast = await this.Joined();
        await broadcast.SendAsync(Event, new { text = "hi" }, new BroadcastPersistence(ttlSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null));
        (await this.SentFrame()).Should().Equal(this.Frame(metadata, JsonEncoding, Utf8("""{"text":"hi"}""")), "the server reads persist only from the frame metadata, so the JSON payload must go out as given");
    }

    [TestMethod]
    public async Task SendAsync_ShouldPushTheBytesBinaryEncoded_GivenAByteArray()
    {
        byte[] reused = [.. Bytes];
        var broadcast = await this.Joined();
        await broadcast.SendAsync(Event, reused, new BroadcastPersistence());
        Array.Clear(reused);
        (await this.SentFrame()).Should().Equal(this.Frame(StoreForTheMaximumRetention, BinaryEncoding, Bytes), "a byte array must go out with the binary encoding, as it was at the call");
    }

    [TestMethod]
    public async Task SendAsync_ShouldComplete_GivenAnOkReply()
    {
        var broadcast = await this.Joined(acknowledged: true);
        var send = broadcast.SendAsync(Event, Bytes, new BroadcastPersistence());
        this.socket.Reply(Topic, this.channel.LastPush!.Ref);
        await send.Awaiting(s => s).Should().CompleteWithinAsync(Patience, "an ok reply means the server stored the message");
    }

    [TestMethod]
    [DataRow("persistence_unauthorized", FailureHint.Reason.PersistenceUnauthorized)]
    [DataRow("persistence_failed", FailureHint.Reason.PersistenceFailed)]
    [DataRow("persistence_invalid_ttl", FailureHint.Reason.PersistenceInvalidTtl)]
    [DataRow("persistence_unheard_of", FailureHint.Reason.Unknown)]
    public async Task SendAsync_ShouldThrowTheServerCode_GivenAnErrorReply(string code, FailureHint.Reason reason)
    {
        var broadcast = await this.Joined(acknowledged: true);
        var send = broadcast.SendAsync(Event, Bytes, new BroadcastPersistence());
        this.socket.Reply(Topic, this.channel.LastPush!.Ref, "error", $$"""{"error":"{{code}}"}""");
        var exception = (await send.Awaiting(s => s).Should().ThrowWithinAsync<RealtimeException>(Patience)).Which;
        using (new AssertionScope())
        {
            exception.Reason.Should().Be(reason, "the server names why it did not store the message, and a code this client does not know must still be accepted");
            exception.Message.Should().Contain(code, "the caller must see the code the server sent");
        }
    }

    [TestMethod]
    public async Task SendAsync_ShouldThrowAndSendNothing_GivenAPublicChannel()
    {
        var broadcast = await this.Joined(isPrivate: false);
        (await broadcast.Awaiting(b => b.SendAsync(Event, Bytes, new BroadcastPersistence())).Should().ThrowAsync<RealtimeException>()).WithMessage("A persisted broadcast needs a private channel.", "the server ignores persist on a public channel and still replies ok");
        this.channel.LastPush.Should().BeNull("nothing must be sent for a message the server would not store");
    }

    [TestMethod]
    public async Task SendAsync_ShouldThrowAndSendNothing_GivenProtocolV1()
    {
        this.options.ProtocolVersion = ProtocolVersion.V1;
        var broadcast = await this.Joined();
        (await broadcast.Awaiting(b => b.SendAsync(Event, Bytes, new BroadcastPersistence())).Should().ThrowAsync<RealtimeException>()).WithMessage("A persisted broadcast needs ClientOptions.ProtocolVersion set to V2.", "only the binary frame of protocol 2.0.0 carries the metadata the server reads persist from");
        this.channel.LastPush.Should().BeNull("nothing must be sent for a message the server would not store");
    }

    [TestMethod]
    public async Task SendAsync_ShouldThrowAndSendNothing_GivenABroadcastModel()
    {
        var broadcast = await this.Joined();
        (await broadcast.Awaiting(b => b.SendAsync(Event, new BroadcastExample { UserId = "1" }, new BroadcastPersistence())).Should().ThrowAsync<ArgumentException>()).WithMessage("A persisted broadcast takes the payload itself, not a BaseBroadcast.*", "the frame already carries the event, so a model would reach receivers nested inside the payload");
        this.channel.LastPush.Should().BeNull("nothing must be sent for a payload the receivers would not read");
    }

    [TestMethod]
    public async Task SendAsync_ShouldThrow_GivenAnEventNameOverTheLongestSize()
    {
        var broadcast = await this.Joined();
        (await broadcast.Awaiting(b => b.SendAsync(new string('e', LongestFieldInBytes + 1), Bytes, new BroadcastPersistence())).Should().ThrowAsync<ArgumentException>()).WithMessage("The event name of a persisted broadcast must fit in 255 bytes.*", "a binary frame has one byte for the size of the event name");
    }

    [TestMethod]
    public async Task SendAsync_ShouldThrow_GivenATopicOverTheLongestSize()
    {
        var broadcast = await this.Joined(new string('t', LongestFieldInBytes));
        (await broadcast.Awaiting(b => b.SendAsync(Event, Bytes, new BroadcastPersistence())).Should().ThrowAsync<RealtimeException>()).WithMessage("The topic of a persisted broadcast must fit in 255 bytes.", "a binary frame has one byte for the size of the topic");
    }

    [TestMethod]
    public async Task SendAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        var broadcast = await this.Joined(acknowledged: true);
        BlockingCaller.Completes(() =>
        {
            var sending = broadcast.SendAsync(Event, Bytes, new BroadcastPersistence());
            Task.Run(() => this.socket.Reply(Topic, this.channel.LastPush!.Ref));
            return sending;
        }).Should().BeTrue("the SDK must not resume on the caller's synchronization context");
    }

    private async Task<RealtimeBroadcast<BaseBroadcast>> Joined(string channelName = "room", bool isPrivate = true, bool acknowledged = false)
    {
        var firstHeartbeat = this.socket.SentText("heartbeat", 1);
        await this.client.ConnectAsync();
        await firstHeartbeat.WaitAsync(Patience);
        this.channel = isPrivate ? this.client.PrivateChannel(channelName) : this.client.Channel(channelName);
        var broadcast = this.channel.Register<BaseBroadcast>(broadcastAck: acknowledged);
        await this.channel.Subscribe().WaitAsync(Patience);
        return broadcast;
    }

    private Task<byte[]> SentFrame() => this.socket.SentBinary.Task.WaitAsync(Patience);

    private byte[] Frame(string metadata, byte encoding, byte[] payload)
    {
        var joinRef = this.channel.JoinPush!.Ref!;
        var reference = this.channel.LastPush!.Ref!;
        return [UserBroadcastPush, (byte) joinRef.Length, (byte) reference.Length, (byte) Topic.Length, (byte) Event.Length, (byte) metadata.Length, encoding, .. Utf8(joinRef + reference + Topic + Event + metadata), .. payload];
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
