using System;
using System.Collections.Generic;
using Websocket.Client;

namespace Supabase.Realtime.Sockets;

/// <summary>
/// Builds the WebSocket transport a <see cref="RealtimeSocket"/> connects through. Implement this to
/// swap in a platform-specific transport — for example a browser-native WebSocket adapter for Unity
/// IL2CPP/WebGL builds, where <see cref="System.Net.WebSockets.ClientWebSocket"/> isn't available.
/// Leave <see cref="ClientOptions.WebSocketFactory"/> unset to use the default
/// <see cref="System.Net.WebSockets.ClientWebSocket"/>-backed transport.
/// </summary>
public interface IWebSocketFactory
{
    /// <summary>
    /// Creates a client targeting <paramref name="uri"/>, not yet started. <paramref name="headers"/> is
    /// invoked at connect/reconnect time (not just once), so the returned client should call it fresh on
    /// each connection attempt rather than caching its result, matching the caller's own live
    /// header-resolution semantics (e.g. dynamic auth headers).
    /// </summary>
    IWebsocketClient Create(Uri uri, Func<Dictionary<string, string>> headers);
}
