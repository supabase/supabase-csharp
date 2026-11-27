using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;

namespace Realtime.Tests.Socket;

/// <summary>
///     Pushes go out inline, in order, without a thread-pool task, so single-threaded hosts (Unity WebGL) can send.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PushDispatchTests
{
    [TestMethod]
    public async Task Push_ShouldSendBeforeReturning_GivenAnOpenSocket()
    {
        var connection = await Connected();
        connection.Socket.Push(Request("first"));
        Events(connection.Client).Should().Contain("first", "an open socket sends the push inline, not on a background task");
    }

    [TestMethod]
    public async Task Push_ShouldSendInCallOrder_GivenAnOpenSocket()
    {
        var connection = await Connected();
        foreach (var name in new[] { "a", "b", "c", "d" })
            connection.Socket.Push(Request(name));
        Events(connection.Client).Where(e => e != "heartbeat").Should().Equal(new[] { "a", "b", "c", "d" }, "pushes keep their order");
    }

    [TestMethod]
    public async Task Push_ShouldFlushInOrderOnConnect_GivenAClosedSocket()
    {
        var client = new FakeWebsocketClient { OpenOnStart = true };
        var socket = NewSocket(client, SerializeAndSend);
        socket.Push(Request("queued-1"));
        socket.Push(Request("queued-2"));
        client.Sent.Should().BeEmpty("nothing is sent before the socket opens");
        await socket.Connect();
        Events(client).Where(e => e.StartsWith("queued")).Should().Equal(new[] { "queued-1", "queued-2" },
            "buffered pushes flush in order once the socket opens");
    }

    [TestMethod]
    public async Task Push_ShouldFlushEveryBufferedPush_GivenAPushBufferedWhileFlushing()
    {
        var client = new FakeWebsocketClient { OpenOnStart = true };
        RealtimeSocket? socket = null;
        socket = NewSocket(client, (payload, send) =>
        {
            SerializeAndSend(payload, send);
            if (payload is SocketRequest { Event: "queued-1" })
                DropAndPush(client, socket!, "late");
        });
        socket.Push(Request("queued-1"));
        socket.Push(Request("queued-2"));
        await socket.Connect();
        Events(client).Where(e => e.StartsWith("queued")).Should().Equal(new[] { "queued-1", "queued-2" },
            "a push buffered by a drop mid-flush must not abort the flush of the pushes already taken");
    }

    [TestMethod]
    public async Task Push_ShouldLogAndNotThrow_GivenAnEncoderThatFails()
    {
        var failure = new JsonException("boom");
        var client = new FakeWebsocketClient { OpenOnStart = true };
        var socket = NewSocket(client, (_, _) => throw failure);
        await socket.Connect();
        var logged = new List<LoggedEvent>();
        IRealtimeDebugger.DebugEventHandler handler = (_, message, error) => { lock (logged) logged.Add(new LoggedEvent(message, error)); };
        Debugger.Instance.AddDebugHandler(handler);
        try
        {
            socket.Invoking(s => s.Push(Request("bad"))).Should().NotThrow("a failed encode was never surfaced to the caller");
        }
        finally
        {
            Debugger.Instance.RemoveDebugHandler(handler);
        }

        lock (logged)
            logged.Should().ContainSingle(l => l.Message.StartsWith("Socket Push failed") && l.Message.Contains("event: bad"), "the failure is logged instead of thrown")
                .Which.Should().Match<LoggedEvent>(l => l.Message.Contains("topic: realtime:test") && ReferenceEquals(l.Error, failure),
                    "the log names the topic and event and carries the encoder exception");
    }

    private sealed record Connection(RealtimeSocket Socket, FakeWebsocketClient Client);

    private sealed record LoggedEvent(string Message, Exception? Error);

    private static async Task<Connection> Connected()
    {
        var client = new FakeWebsocketClient { OpenOnStart = true };
        var socket = NewSocket(client, SerializeAndSend);
        await socket.Connect();
        return new Connection(socket, client);
    }

    private static RealtimeSocket NewSocket(FakeWebsocketClient client, Action<object, Action<string>> encode) =>
        new("ws://localhost:4000/socket", new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(client),
            Encode = encode,
        });

    private static void SerializeAndSend(object payload, Action<string> send) => send(JsonSerializer.Serialize(payload));

    private static void DropAndPush(FakeWebsocketClient client, RealtimeSocket socket, string name)
    {
        client.IsRunning = false;
        socket.Push(Request(name));
    }

    private static SocketRequest Request(string name) => new() { Topic = "realtime:test", Event = name, Payload = new { } };

    private static string[] Events(FakeWebsocketClient client)
    {
        lock (client.Sent)
            return client.Sent.Select(m => JsonDocument.Parse(m).RootElement.GetProperty("event").GetString()!).ToArray();
    }
}
