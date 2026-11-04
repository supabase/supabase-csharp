using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
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
}
