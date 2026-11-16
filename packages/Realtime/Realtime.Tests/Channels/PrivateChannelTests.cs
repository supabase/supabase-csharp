using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

namespace Realtime.Tests.Channels;

/// <summary>
///     <see cref="Client.PrivateChannel" /> joins a private channel with the client's own current access token, so
///     an RLS policy on <c>realtime.messages</c> sees the signed-in user without the caller wiring a token getter
///     (realtime.subscriptions.private_channel).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class PrivateChannelTests
{
    private const string UserJwt = "a-user-jwt";
    private Client client = null!;

    [TestInitialize]
    public async Task ConnectWithUserToken()
    {
        this.client = new Client("ws://localhost/socket", new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(new FakeWebsocketClient { OpenOnStart = true }),
        });
        await this.client.ConnectAsync();
        this.client.SetAuth(UserJwt);
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public void PrivateChannel_ShouldJoinAsPrivate() =>
        JoinFrame(this.client.PrivateChannel("user:a-user-id"))["config"]!["private"]!.GetValue<bool>().Should()
            .BeTrue("the server authorizes only a channel flagged private against its RLS policies");

    [TestMethod]
    public void PrivateChannel_ShouldJoinWithTheClientAccessToken() =>
        JoinFrame(this.client.PrivateChannel("user:a-user-id"))["access_token"]!.GetValue<string>().Should()
            .Be(UserJwt, "the join is authorized as the user whose token the client holds (#400)");

    [TestMethod]
    public void PrivateChannel_ShouldJoinWithTheLatestToken_GivenSetAuthAfterTheChannelWasAdded()
    {
        var channel = this.client.PrivateChannel("user:a-user-id");
        this.client.SetAuth("a-refreshed-jwt");
        JoinFrame(channel)["access_token"]!.GetValue<string>().Should()
            .Be("a-refreshed-jwt", "a rejoin after a token refresh must not reuse the token the channel was created with");
    }

    private static JsonObject JoinFrame(RealtimeChannel channel)
    {
        var payload = channel.GenerateJoinPush().Payload!;
        return JsonNode.Parse(JsonSerializer.Serialize(payload, payload.GetType(), Wire.Settings()))!.AsObject();
    }
}
