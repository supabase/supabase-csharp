using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;

namespace Realtime.Tests.Channels;

/// <summary>
///     Regression cover for supabase-csharp#400: a private channel is authorized against RLS during the
///     initial <c>phx_join</c>, so the current user's JWT has to travel inside that join frame. The reference
///     realtime-js SDK writes it as a top-level <c>access_token</c> sibling of <c>config</c> on every
///     (re)subscribe; the C# port shipped private channels (7.4.0, #61) with <c>config.private</c> but without
///     that field, so authenticated joins were rejected before the post-join auth push could run. These pin the
///     join frame the channel actually generates from its <see cref="Supabase.Realtime.Channel.ChannelOptions.RetrieveAccessToken" />.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class JoinAccessTokenTests
{
    private static JsonObject JoinFrame(Supabase.Realtime.RealtimeChannel channel)
    {
        var payload = channel.GenerateJoinPush().Payload!;
        return JsonNode.Parse(JsonSerializer.Serialize(payload, payload.GetType(), Wire.Settings()))!.AsObject();
    }

    [TestMethod]
    public void GenerateJoinPush_ShouldCarryAccessToken_GivenPrivateChannelWithToken()
    {
        var channel = Wire.Channel("realtime:private:todos", Wire.PrivateOptions("jwt-123"));

        var frame = JoinFrame(channel);

        frame.ContainsKey("access_token").Should()
            .BeTrue("a private channel is authorized during phx_join, so the JWT must be in the join frame (#400)");
        frame["access_token"]!.GetValue<string>().Should().Be("jwt-123");
        frame.ContainsKey("config").Should()
            .BeTrue("access_token is a sibling of config, not a replacement for it");
    }

    [TestMethod]
    public void GenerateJoinPush_ShouldCarryAccessToken_GivenPublicChannelWithToken()
    {
        var channel = Wire.Channel("realtime:public:todos", Wire.PublicOptions("jwt-123"));

        JoinFrame(channel)["access_token"]!.GetValue<string>().Should()
            .Be("jwt-123", "realtime-js attaches the access token to every subscribe, public or private");
    }

    [TestMethod]
    public void GenerateJoinPush_ShouldOmitAccessToken_GivenNoToken()
    {
        var channel = Wire.Channel("realtime:private:todos", Wire.PrivateOptions(accessToken: null));

        JoinFrame(channel).ContainsKey("access_token").Should()
            .BeFalse("an anonymous join must not send an empty access_token field");
    }
}
