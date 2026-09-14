using System.Text.Json;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;

namespace Realtime.Tests.Support;

/// <summary>
///     Shared builders for the hermetic channel tests. The seam is a real, <em>unconnected</em>
///     <see cref="RealtimeSocket" />: constructing it opens no connection, so a channel can be exercised in
///     process without a live Phoenix server and without a hand-written stub. Inbound routing is driven by
///     calling <c>RealtimeChannel.HandleSocketMessage</c> with a frame from <see cref="Decode" />; the
///     subscribe/send handshake (which needs the server to reply) stays in the E2E tier.
/// </summary>
internal static class Wire
{
    private const string Endpoint = "ws://127.0.0.1:54321/realtime/v1";

    public static JsonSerializerOptions Settings() => RealtimeSerializerOptions.Build(new ClientOptions());

    public static ChannelOptions PublicOptions() =>
        ChannelOptions.Public(new ClientOptions(), () => null, Settings());

    public static ChannelOptions PrivateOptions() =>
        ChannelOptions.Private(new ClientOptions(), () => null, Settings());

    public static ChannelOptions PrivateOptions(string? accessToken) =>
        ChannelOptions.Private(new ClientOptions(), () => accessToken, Settings());

    public static ChannelOptions PublicOptions(string? accessToken) =>
        ChannelOptions.Public(new ClientOptions(), () => accessToken, Settings());

    public static IRealtimeSocket Socket() => new RealtimeSocket(Endpoint, new ClientOptions());

    public static RealtimeChannel Channel(string topic = "realtime:example", ChannelOptions? options = null) =>
        new(Socket(), topic, options ?? PublicOptions());

    /// <summary>
    ///     Reproduces the client's default decoder: populate a <see cref="SocketResponse" /> from the frame and
    ///     stamp the raw JSON, which the typed re-parsing in broadcast/presence/postgres_changes relies on.
    /// </summary>
    public static SocketResponse Decode(string json)
    {
        var settings = Settings();
        var response = JsonSerializer.Deserialize<SocketResponse>(json, settings) ?? new SocketResponse(settings);
        response.SerializerSettings = settings;
        response.Json = json;
        return response;
    }
}
