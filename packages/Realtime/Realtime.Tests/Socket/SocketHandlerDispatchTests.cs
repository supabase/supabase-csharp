using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Socket;

/// <summary>
///     The socket reports state changes, messages, heartbeats and errors to registered handlers,
///     and a handler removed or cleared stops being called.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class SocketHandlerDispatchTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly string Frame = DataFile.Read("Socket", "BroadcastFrame.json");

    private RealtimeSocket socket = null!;
    private int calls;

    [TestCleanup]
    public void Disconnect() => this.socket.Disconnect();

    [TestMethod]
    public async Task StateHandler_ShouldBeCalledWithOpen_GivenAConnection()
    {
        var states = new List<SocketState>();
        this.SocketOver(new FakeWebsocketClient { OpenOnStart = true }).AddStateChangedHandler((_, state) => states.Add(state));

        await this.socket.Connect();

        states.Should().Contain(SocketState.Open, "a state handler hears that the socket opened");
    }

    [TestMethod]
    public async Task StateHandler_ShouldNotBeCalled_GivenItWasCleared()
    {
        this.SocketOver(new FakeWebsocketClient { OpenOnStart = true }).AddStateChangedHandler((_, _) => this.calls++);
        this.socket.ClearStateChangedHandlers();

        await this.socket.Connect();

        this.calls.Should().Be(0, "a cleared handler is not notified");
    }

    [TestMethod]
    public async Task StateHandler_ShouldBeCalledWithError_GivenAFailureBeforeTheFirstConnection()
    {
        var server = new FakeWebsocketClient();
        var states = new List<SocketState>();
        this.SocketOver(server).AddStateChangedHandler((_, state) => states.Add(state));
        await this.socket.Connect();

        server.Fail(new InvalidOperationException("boom"));

        states.Should().Contain(SocketState.Error, "a transport failure puts the socket in the error state");
    }

    [TestMethod]
    public async Task MessageHandler_ShouldReceiveTheFrame_GivenAServerMessage()
    {
        var server = new FakeWebsocketClient { OpenOnStart = true };
        string? topic = null;
        this.SocketOver(server).AddMessageReceivedHandler((_, message) => topic = message.Topic);
        await this.socket.Connect();

        server.Receive(Frame);

        topic.Should().Be("room", "a message handler gets the decoded frame");
    }

    [TestMethod]
    public async Task MessageHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var server = new FakeWebsocketClient { OpenOnStart = true };
        IRealtimeSocket.MessageEventHandler handler = (_, _) => this.calls++;
        this.SocketOver(server).AddMessageReceivedHandler(handler);
        this.socket.RemoveMessageReceivedHandler(handler);
        await this.socket.Connect();

        server.Receive(Frame);

        this.calls.Should().Be(0, "a removed handler is not notified");
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldBeCalled_GivenAHeartbeatReply()
    {
        var heard = new TaskCompletionSource();
        this.SocketOver(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true })
            .AddHeartbeatHandler((_, _) => heard.TrySetResult());

        await this.socket.Connect();

        await heard.Awaiting(source => source.Task).Should().CompleteWithinAsync(Patience,
            "a heartbeat handler hears the server's reply");
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldNotBeCalled_GivenItWasCleared()
    {
        var witnessed = new TaskCompletionSource();
        this.SocketOver(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true })
            .AddHeartbeatHandler((_, _) => this.calls++);
        this.socket.ClearHeartbeatHandlers();
        this.socket.AddHeartbeatHandler((_, _) => witnessed.TrySetResult());

        await this.socket.Connect();
        await witnessed.Task.WaitAsync(Patience);

        this.calls.Should().Be(0, "a cleared handler is not notified of the reply its successor heard");
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldBeCalled_GivenAFailureBeforeTheFirstConnection()
    {
        var server = new FakeWebsocketClient();
        var errors = new List<Exception>();
        this.SocketOver(server).AddErrorHandler((_, exception) => errors.Add(exception));
        await this.socket.Connect();

        server.Fail(new InvalidOperationException("boom"));

        errors.Should().ContainSingle("the error handler hears the failure");
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldNotBeCalled_GivenItWasRemoved()
    {
        var server = new FakeWebsocketClient();
        IRealtimeSocket.ErrorEventHandler handler = (_, _) => this.calls++;
        this.SocketOver(server).AddErrorHandler(handler);
        this.socket.RemoveErrorHandler(handler);
        await this.socket.Connect();

        server.Fail(new InvalidOperationException("boom"));

        this.calls.Should().Be(0, "a removed handler is not notified");
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldNotBeCalled_GivenItWasCleared()
    {
        var server = new FakeWebsocketClient();
        this.SocketOver(server).AddErrorHandler((_, _) => this.calls++);
        this.socket.ClearErrorHandlers();
        await this.socket.Connect();

        server.Fail(new InvalidOperationException("boom"));

        this.calls.Should().Be(0, "a cleared handler is not notified");
    }

    [TestMethod]
    public async Task MessageHandler_ShouldNotStopTheNextHandler_GivenItThrows()
    {
        var server = new FakeWebsocketClient { OpenOnStart = true };
        var failure = new InvalidOperationException("boom");
        Exception? logged = null;
        Debugger.Instance.AddDebugHandler((_, _, error) => logged ??= error);
        this.SocketOver(server).AddMessageReceivedHandler((_, _) => throw failure);
        this.socket.AddMessageReceivedHandler((_, _) => this.calls++);
        await this.socket.Connect();
        server.Receive(Frame);
        Debugger.Instance.ClearDebugHandlers();
        this.calls.Should().Be(1, "a throwing handler must not stop the next one");
        logged.Should().BeSameAs(failure, "the handler's exception is logged, not lost");
    }

    [TestMethod]
    public async Task HeartbeatHandler_ShouldNotStopTheNextHandler_GivenItThrows()
    {
        var heard = new TaskCompletionSource();
        this.SocketOver(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true })
            .AddHeartbeatHandler((_, _) => throw new InvalidOperationException("boom"));
        this.socket.AddHeartbeatHandler((_, _) => heard.TrySetResult());
        await this.socket.Connect();
        await heard.Awaiting(source => source.Task).Should().CompleteWithinAsync(Patience, "a throwing handler must not stop the next one");
    }

    [TestMethod]
    public async Task StateHandler_ShouldNotStopTheNextHandler_GivenItThrows()
    {
        this.SocketOver(new FakeWebsocketClient { OpenOnStart = true }).AddStateChangedHandler((_, _) => throw new InvalidOperationException("boom"));
        this.socket.AddStateChangedHandler((_, _) => this.calls++);
        await this.socket.Connect();
        this.calls.Should().Be(1, "a throwing handler must not stop the next one");
    }

    [TestMethod]
    public async Task ErrorHandler_ShouldNotStopTheNextHandler_GivenItThrows()
    {
        var server = new FakeWebsocketClient();
        this.SocketOver(server).AddErrorHandler((_, _) => throw new InvalidOperationException("boom"));
        this.socket.AddErrorHandler((_, _) => this.calls++);
        await this.socket.Connect();
        server.Fail(new InvalidOperationException("lost"));
        this.calls.Should().Be(1, "a throwing handler must not stop the next one");
    }

    private RealtimeSocket SocketOver(FakeWebsocketClient server)
    {
        this.socket = new RealtimeSocket("ws://localhost/socket", Wire.OptionsOver(server));
        return this.socket;
    }
}
