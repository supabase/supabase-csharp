using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Supabase.Realtime;
using Supabase.Realtime.Sockets;
using Websocket.Client;

namespace Realtime.Tests.Support;

internal sealed class FakeWebSocketFactory(IWebsocketClient client) : IWebSocketFactory
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

/// <summary>Implements only the <see cref="IWebsocketClient"/> members <see cref="RealtimeSocket"/> actually touches; the rest are unreachable from these tests and throw if hit.</summary>
internal sealed class FakeWebsocketClient : IWebsocketClient
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

    /// <summary>When set, <c>StartOrFail</c> reports the client as connected.</summary>
    public bool OpenOnStart { get; init; }

    public Task StartOrFail()
    {
        this.StartOrFailCallCount++;
        if (this.OpenOnStart)
        {
            this.IsRunning = true;
            this.reconnectionHappened.OnNext(ReconnectionInfo.Create(ReconnectionType.Initial));
        }

        return Task.CompletedTask;
    }

    public Task<bool> Stop(WebSocketCloseStatus status, string reason)
    {
        this.IsRunning = false;
        return Task.FromResult(true);
    }

    /// <summary>Completes when the socket sends its first frame, the heartbeat that starts on open.</summary>
    public TaskCompletionSource FirstSend { get; } = new();

    /// <summary>When set, every heartbeat gets its reply, as from a healthy server.</summary>
    public bool AnswerHeartbeats { get; init; }

    /// <summary>Completes when the socket forces a reconnect.</summary>
    public TaskCompletionSource Reconnected { get; } = new();

    public bool Send(string message)
    {
        this.FirstSend.TrySetResult();
        if (this.AnswerHeartbeats && JsonNode.Parse(message) is { } frame && (string?) frame["event"] == "heartbeat")
            this.Receive("""{"topic":"phoenix","event":"phx_reply","ref":"REF","payload":{"status":"ok","response":{}}}""".Replace("REF", (string?) frame["ref"]));
        return true;
    }

    public Task Reconnect()
    {
        this.Reconnected.TrySetResult();
        return Task.CompletedTask;
    }

    /// <summary>Delivers a frame as if the server had sent it.</summary>
    public void Receive(string json) => this.messageReceived.OnNext(ResponseMessage.TextMessage(json));

    public void Dispose() { }

    // Unused by RealtimeSocket, not exercised by these tests.
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
