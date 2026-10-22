using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Sockets;
using Websocket.Client;

namespace Realtime.Tests.Socket;

/// <summary>
/// Contract tests pinning that <see cref="RealtimeSocket"/> drives an injected <see cref="IWebSocketFactory"/>
/// through the same seam the default <see cref="System.Net.WebSockets.ClientWebSocket"/>-backed transport uses,
/// rather than constructing a <see cref="WebsocketClient"/> directly — this is what makes the transport
/// swappable (e.g. for a future Unity IL2CPP/WebGL adapter) without touching <see cref="RealtimeSocket"/> itself.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class WebSocketFactoryInjectionTests
{
    [TestMethod]
    public void Constructor_ShouldCallTheInjectedFactory_GivenAWebSocketFactoryOption()
    {
        var fakeClient = new FakeWebsocketClient();
        var factory = new FakeWebSocketFactory(fakeClient);

        var options = new ClientOptions { WebSocketFactory = factory, Parameters = { ApiKey = "test-key" } };
        _ = new RealtimeSocket("ws://localhost:4000/socket", options);

        factory.CreatedUri.Should().NotBeNull("the socket must build its transport through the injected factory");
        factory.CreatedUri!.ToString().Should().Contain("apikey=test-key",
            "the factory must receive the fully-formed endpoint, including auth query params");
    }

    [TestMethod]
    public async Task Connect_ShouldStartTheInjectedClientNotADefaultTransport()
    {
        var fakeClient = new FakeWebsocketClient();
        var factory = new FakeWebSocketFactory(fakeClient);
        var socket = new RealtimeSocket("ws://localhost:4000/socket", new ClientOptions { WebSocketFactory = factory });

        await socket.Connect();

        fakeClient.StartOrFailCallCount.Should().Be(1,
            "Connect() must start the client the factory produced, not fall back to a default transport");
    }

    [TestMethod]
    public void IsConnected_ShouldReflectTheInjectedClientsIsRunning()
    {
        var fakeClient = new FakeWebsocketClient { IsRunning = true };
        var factory = new FakeWebSocketFactory(fakeClient);
        var socket = new RealtimeSocket("ws://localhost:4000/socket", new ClientOptions { WebSocketFactory = factory });

        socket.IsConnected.Should().BeTrue("IsConnected must be sourced from the injected client, not a hardcoded default");
    }

    [TestMethod]
    public void HeadersProvider_ShouldReEvaluateLazily_GivenALateSetGetHeaders()
    {
        var fakeClient = new FakeWebsocketClient();
        var factory = new FakeWebSocketFactory(fakeClient);
        var socket = new RealtimeSocket("ws://localhost:4000/socket", new ClientOptions { WebSocketFactory = factory });

        // Mirrors Supabase.Realtime.Client.ConnectAsync(): GetHeaders is assigned to the socket
        // *after* construction, so the factory's headers provider must re-invoke rather than have
        // captured a stale (empty) snapshot at construction time.
        socket.GetHeaders = () => new Dictionary<string, string> { { "X-Late", "yes" } };

        factory.HeadersProvider.Should().NotBeNull();
        factory.HeadersProvider!().Should().ContainKey("X-Late",
            "the header provider must be re-invoked at connect time so headers set after construction are honored");
    }
}
