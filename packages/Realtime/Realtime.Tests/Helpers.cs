using System.Collections.Generic;
using System.Diagnostics;
using Supabase.Realtime;
using Supabase.Realtime.Socket;
using Client = Supabase.Realtime.Client;

namespace Realtime.Tests;

internal static class Helpers
{
    public const string ApiKeyAnon = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";
    public const string ApiKey = "sb_secret_N7UND0UgjKTVK-Uodkm0Hg_xSvEMPvz";

    private const string SocketEndpoint = "ws://127.0.0.1:54321/realtime/v1";
    private const string RestEndpoint = "http://localhost:54321/rest/v1";

    public static Supabase.Postgrest.Client RestClient() => new(RestEndpoint, new Supabase.Postgrest.ClientOptions());

    public static Client SocketClient(ProtocolVersion protocolVersion = ProtocolVersion.V1)
    {
        var client = new Client(SocketEndpoint, new ClientOptions
        {
            ProtocolVersion = protocolVersion,
            Parameters = new SocketOptionsParameters
            {
                ApiKey = ApiKeyAnon
            }
        });

        client.AddDebugHandler((_, message, _) => Debug.WriteLine(message));

        return client;
    }

    public static Client PrivateSocketClient()
    {
        var client = new Client(SocketEndpoint, new ClientOptions
        {
            Parameters = new SocketOptionsParameters
            {
                ApiKey = ApiKey
            }
        });

        client.AddDebugHandler((_, message, _) => Debug.WriteLine(message));

        return client;
    }
}
