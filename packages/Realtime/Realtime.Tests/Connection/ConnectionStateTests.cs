using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Connection;

/// <summary>
///     The client reports whether its connection is closed, connecting or open. Runs over a fake socket, so
///     no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ConnectionStateTests
{
    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true };
    private Client client = null!;

    [TestInitialize]
    public void CreateClient() =>
        this.client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });

    [TestMethod]
    public void ConnectionState_ShouldBeClosed_GivenNoConnectYet() =>
        this.client.ConnectionState.Should().Be(ConnectionState.Closed);

    [TestMethod]
    public void ConnectionState_ShouldBeConnecting_GivenTheHandshakeIsInFlight()
    {
        var pending = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(new FakeWebsocketClient { Handshake = new TaskCompletionSource() }) });
        _ = pending.ConnectAsync();
        pending.ConnectionState.Should().Be(ConnectionState.Connecting);
    }

    [TestMethod]
    public async Task ConnectionState_ShouldBeOpen_GivenConnected()
    {
        await this.client.ConnectAsync();
        this.client.ConnectionState.Should().Be(ConnectionState.Open);
    }

    [TestMethod]
    public async Task ConnectionState_ShouldBeConnecting_GivenALostConnection()
    {
        await this.client.ConnectAsync();
        this.socket.Fail(new InvalidOperationException("boom"));
        this.client.ConnectionState.Should().Be(ConnectionState.Connecting);
    }

    [TestMethod]
    public async Task ConnectionState_ShouldBeOpen_GivenALostConnectionCameBack()
    {
        await this.client.ConnectAsync();
        this.socket.Fail(new InvalidOperationException("boom"));
        this.socket.RecoverFromLostConnection();
        this.client.ConnectionState.Should().Be(ConnectionState.Open);
    }

    [TestMethod]
    public async Task ConnectionState_ShouldBeClosed_GivenDisconnect()
    {
        await this.client.ConnectAsync();
        this.client.Disconnect();
        this.client.ConnectionState.Should().Be(ConnectionState.Closed);
    }
}
