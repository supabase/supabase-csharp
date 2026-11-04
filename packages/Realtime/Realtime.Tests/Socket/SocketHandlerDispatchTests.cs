using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using static Supabase.Realtime.Constants;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;

namespace Realtime.Tests.Socket;

/// <summary>
///     The socket reports state changes, messages, heartbeats and errors to registered handlers,
///     and a handler removed or cleared stops being called.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SocketHandlerDispatchTests
{
    private const string Frame = """{"topic":"room","event":"broadcast","ref":"1","payload":{}}""";

    private static (RealtimeSocket Socket, FakeWebsocketClient Fake) Create(bool openOnStart = true, bool answerHeartbeats = false)
    {
        var fake = new FakeWebsocketClient { OpenOnStart = openOnStart, AnswerHeartbeats = answerHeartbeats };
        var options = new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(fake), HeartbeatInterval = TimeSpan.FromMilliseconds(20) };
        _ = new Client("ws://localhost/socket", options); // installs the default message encoder and decoder on the options
        return (new RealtimeSocket("ws://localhost/socket", options), fake);
    }

    [TestMethod]
    public async Task StateHandler_ShouldBeCalledWithOpen_GivenAConnection()
    {
        var (socket, _) = Create();
        var states = new List<SocketState>();
        socket.AddStateChangedHandler((_, state) => states.Add(state));

        await socket.Connect();

        states.Should().Contain(SocketState.Open, "a state handler hears that the socket opened");
        socket.Disconnect();
    }

    [TestMethod]
    public async Task StateHandler_ShouldNotBeCalled_GivenItWasCleared()
    {
        var (socket, _) = Create();
        var calls = 0;
        socket.AddStateChangedHandler((_, _) => calls++);
        socket.ClearStateChangedHandlers();

        await socket.Connect();

        calls.Should().Be(0, "a cleared handler is not notified");
        socket.Disconnect();
    }

    [TestMethod]
    public async Task MessageHandler_ShouldReceiveTheFrame_GivenAServerMessage()
    {
        var (socket, fake) = Create();
        SocketResponse? received = null;
        socket.AddMessageReceivedHandler((_, message) => received = message);
        await socket.Connect();

        fake.Receive(Frame);

        received.Should().NotBeNull("a message handler gets the decoded frame");
        received!.Topic.Should().Be("room");
        socket.Disconnect();
    }

    [TestMethod]
    public async Task MessageHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var (socket, fake) = Create();
        var calls = 0;
        IRealtimeSocket.MessageEventHandler handler = (_, _) => calls++;
        socket.AddMessageReceivedHandler(handler);
        socket.RemoveMessageReceivedHandler(handler);
        await socket.Connect();

        fake.Receive(Frame);

        calls.Should().Be(0, "a removed handler is not notified");
        socket.Disconnect();
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldBeCalled_GivenAHeartbeatReply()
    {
        var (socket, _) = Create(answerHeartbeats: true);
        var heard = new TaskCompletionSource();
        socket.AddHeartbeatHandler((_, _) => heard.TrySetResult());

        await socket.Connect();

        await heard.Task.Awaiting(t => t).Should().CompleteWithinAsync(TimeSpan.FromSeconds(5),
            "a heartbeat handler hears the server's reply");
        socket.Disconnect();
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldNotBeCalled_GivenItWasCleared()
    {
        var (socket, fake) = Create(answerHeartbeats: true);
        var calls = 0;
        socket.AddHeartbeatHandler((_, _) => calls++);
        socket.ClearHeartbeatHandlers();

        await socket.Connect();
        await fake.FirstSend.Task.Awaiting(t => t).Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));

        calls.Should().Be(0, "a cleared handler is not notified");
        socket.Disconnect();
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldBeCalled_GivenAFailureBeforeTheFirstConnection()
    {
        var (socket, fake) = Create(openOnStart: false);
        var errors = new List<Exception>();
        var states = new List<SocketState>();
        socket.AddErrorHandler((_, exception) => errors.Add(exception));
        socket.AddStateChangedHandler((_, state) => states.Add(state));
        await socket.Connect();

        fake.Fail(new InvalidOperationException("boom"));

        errors.Should().ContainSingle("the error handler hears the failure");
        states.Should().Contain(SocketState.Error);
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var (socket, fake) = Create(openOnStart: false);
        var calls = 0;
        IRealtimeSocket.ErrorEventHandler handler = (_, _) => calls++;
        socket.AddErrorHandler(handler);
        socket.RemoveErrorHandler(handler);
        await socket.Connect();

        fake.Fail(new InvalidOperationException("boom"));

        calls.Should().Be(0, "a removed handler is not notified");
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldNotBeCalled_GivenItWasCleared()
    {
        var (socket, fake) = Create(openOnStart: false);
        var calls = 0;
        socket.AddErrorHandler((_, _) => calls++);
        socket.ClearErrorHandlers();
        await socket.Connect();

        fake.Fail(new InvalidOperationException("boom"));

        calls.Should().Be(0, "a cleared handler is not notified");
    }
}
