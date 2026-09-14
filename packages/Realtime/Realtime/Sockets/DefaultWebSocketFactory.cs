using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using Websocket.Client;

namespace Supabase.Realtime.Sockets;

/// <summary>The <see cref="IWebSocketFactory"/> used when <see cref="ClientOptions.WebSocketFactory"/> is left unset.</summary>
internal sealed class DefaultWebSocketFactory : IWebSocketFactory
{
    public static readonly DefaultWebSocketFactory Instance = new();

    private DefaultWebSocketFactory() { }

    public IWebsocketClient Create(Uri uri, Func<Dictionary<string, string>> headers) =>
        new WebsocketClient(uri, () =>
        {
            var socket = new ClientWebSocket();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"))) return socket;

            foreach (var header in headers())
                socket.Options.SetRequestHeader(header.Key, header.Value);

            return socket;
        });
}
