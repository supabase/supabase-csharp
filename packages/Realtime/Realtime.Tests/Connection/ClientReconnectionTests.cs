using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Connection;

/// <summary>
///     When a lost connection comes back, the client's state handlers hear about it, whichever way the client
///     connected. Runs over a fake socket, so no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ClientReconnectionTests
{
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true };
    private readonly List<SocketState> states = [];
    private Client client = null!;

    [TestInitialize]
    public void ListenToStateChanges()
    {
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        this.client.AddStateChangedHandler((_, state) => this.states.Add(state));
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldReportTheReconnect_GivenALostConnectionCameBack()
    {
        await this.client.ConnectAsync();
        this.socket.RecoverFromLostConnection();
        this.states.Should().Equal([SocketState.Open, SocketState.Reconnect, SocketState.Open], "a reconnect must reach the client's state handlers (issue #105)");
    }

    [TestMethod]
    public void Connect_ShouldReportTheReconnect_GivenALostConnectionCameBack()
    {
#pragma warning disable CS0618 // pins the still-shipping obsolete callback Connect
        this.client.Connect();
#pragma warning restore CS0618
        this.socket.RecoverFromLostConnection();
        this.states.Should().Equal([SocketState.Open, SocketState.Reconnect, SocketState.Open], "a reconnect must reach the client's state handlers (issue #105)");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldReportCloseOncePerDrop()
    {
        await this.client.ConnectAsync();
        this.socket.Fail(new InvalidOperationException("boom"));
        this.socket.Fail(new InvalidOperationException("boom"));
        this.socket.RecoverFromLostConnection();
        this.socket.Fail(new InvalidOperationException("boom"));
        this.states.Should().Equal([SocketState.Open, SocketState.Close, SocketState.Reconnect, SocketState.Open, SocketState.Close], "failed reconnect attempts are part of the same drop");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldNotReconnect_GivenAStateHandlerDisconnectsOnClose()
    {
        this.client.AddStateChangedHandler((_, state) =>
        {
            if (state == SocketState.Close)
                this.client.Disconnect();
        });
        await this.client.ConnectAsync();
        this.socket.Fail(new InvalidOperationException("boom"));
        this.socket.IsReconnectionEnabled.Should().BeFalse("the handler already disconnected, so nothing owns this socket");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldReachTheNextStateHandler_GivenOneThrows()
    {
        var failure = new InvalidOperationException("boom");
        var logged = new List<Exception?>();
        var heard = new List<SocketState>();
        this.client.AddDebugHandler((_, _, error) => logged.Add(error));
        this.client.AddStateChangedHandler((_, _) => throw failure);
        this.client.AddStateChangedHandler((_, state) => heard.Add(state));
        await this.client.ConnectAsync();
        this.client.ClearDebugHandlers();
        heard.Should().Equal([SocketState.Open], "a throwing handler must not stop the next one or the connect");
        logged.Should().Contain(failure, "the handler's exception is logged, not lost");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldFail_GivenADisconnectBeforeTheSocketOpened()
    {
        var socket = new FakeWebsocketClient { Handshake = new() };
        var client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket) });
        var connect = client.ConnectAsync();
        client.Disconnect();
        socket.Handshake.SetResult();
        var act = () => connect.WaitAsync(TimeSpan.FromSeconds(5));
        (await act.Should().ThrowAsync<RealtimeException>().WithMessage("Disconnected before the socket opened.")).Which.Reason.Should().Be(FailureHint.Reason.SocketError);
        socket.IsRunning.Should().BeFalse("nothing owns this socket, so it must not stay open");
    }

    [TestMethod]
    public async Task Disconnect_ShouldCloseTheSocket_GivenAReconnectFinishesAfterIt()
    {
        await this.client.ConnectAsync();
        var heard = new List<SocketState>();
        this.client.Socket!.AddStateChangedHandler((_, state) => heard.Add(state));
        this.client.Disconnect();
        this.socket.IsRunning = true;
        this.socket.RecoverFromLostConnection();
        this.socket.IsRunning.Should().BeFalse("nothing owns this socket, so it must not stay open");
        heard.Should().BeEmpty("a socket closed on arrival never opened");
    }
}
