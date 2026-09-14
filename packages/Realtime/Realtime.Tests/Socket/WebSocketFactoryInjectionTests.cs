using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

    private sealed class FakeWebSocketFactory(IWebsocketClient client) : IWebSocketFactory
    {
        public Uri? CreatedUri { get; private set; }
        public Func<Dictionary<string, string>>? HeadersProvider { get; private set; }

        public IWebsocketClient Create(Uri uri, Func<Dictionary<string, string>> headers)
        {
            this.CreatedUri = uri;
            this.HeadersProvider = headers;
            return client;
        }
    }

    /// <summary>Implements only the <see cref="IWebsocketClient"/> members <see cref="RealtimeSocket"/> actually touches; the rest are unreachable from this test and throw if hit.</summary>
    private sealed class FakeWebsocketClient : IWebsocketClient
    {
        public int StartOrFailCallCount { get; private set; }
        public bool IsRunning { get; set; }

        public IObservable<ResponseMessage> MessageReceived => this.messageReceived;
        public IObservable<ReconnectionInfo> ReconnectionHappened => this.reconnectionHappened;
        public IObservable<DisconnectionInfo> DisconnectionHappened => this.disconnectionHappened;

        private readonly Subject<ResponseMessage> messageReceived = new();
        private readonly Subject<ReconnectionInfo> reconnectionHappened = new();
        private readonly Subject<DisconnectionInfo> disconnectionHappened = new();

        public TimeSpan? ReconnectTimeout { get; set; }
        public TimeSpan? ErrorReconnectTimeout { get; set; }

        public Task StartOrFail()
        {
            this.StartOrFailCallCount++;
            return Task.CompletedTask;
        }

        public Task<bool> Stop(WebSocketCloseStatus status, string reason)
        {
            this.IsRunning = false;
            return Task.FromResult(true);
        }

        public bool Send(string message) => true;

        public void Dispose() { }

        // Unused by RealtimeSocket — not exercised by these tests.
        public Uri Url { get; set; } = new("ws://localhost/unused");
        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan? LostReconnectTimeout { get; set; }
        public bool IsReconnectionEnabled { get; set; }
        public string? Name { get; set; }
        public bool IsStarted => this.IsRunning;
        public bool TextSenderRunning => throw new NotImplementedException();
        public bool BinarySenderRunning => throw new NotImplementedException();
        public bool IsInsideLock => throw new NotImplementedException();
        public bool IsTextMessageConversionEnabled { get; set; }
        public bool IsStreamDisposedAutomatically { get; set; }
        public Encoding? MessageEncoding { get; set; } = Encoding.UTF8;
        public ClientWebSocket NativeClient => throw new NotImplementedException();
        public Task Start() => throw new NotImplementedException();
        public Task<bool> StopOrFail(WebSocketCloseStatus status, string reason) => throw new NotImplementedException();
        public Task Reconnect() => throw new NotImplementedException();
        public Task ReconnectOrFail() => throw new NotImplementedException();
        public bool Send(byte[] message) => throw new NotImplementedException();
        public bool Send(ArraySegment<byte> message) => throw new NotImplementedException();
        public bool Send(ReadOnlySequence<byte> message) => throw new NotImplementedException();
        public Task SendInstant(string message) => throw new NotImplementedException();
        public Task SendInstant(byte[] message) => throw new NotImplementedException();
        public bool SendAsText(byte[] message) => throw new NotImplementedException();
        public bool SendAsText(ArraySegment<byte> message) => throw new NotImplementedException();
        public bool SendAsText(ReadOnlySequence<byte> message) => throw new NotImplementedException();
        public void StreamFakeMessage(ResponseMessage message) => throw new NotImplementedException();
    }
}
