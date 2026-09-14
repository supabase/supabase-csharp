using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Presence;

namespace Realtime.Tests.Presence;

/// <summary>
///     Regression tests for presence join configuration: checks that proper config properties are present for
///     different registration paths.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PresenceRegistrationTests
{
    private static JsonObject PresenceConfig(RealtimeChannel channel)
    {
        var payload = channel.GenerateJoinPush().Payload!;
        var frame = JsonNode.Parse(JsonSerializer.Serialize(payload, payload.GetType(), Wire.Settings()))!.AsObject();
        return frame["config"]!["presence"]!.AsObject();
    }

    [TestMethod]
    public void Register_ShouldIncludeEnabledInJoinPayload_GivenStringKey()
    {
        var channel = Wire.Channel();
        channel.Register<PresenceExample>("some-key");

        var presence = PresenceConfig(channel);
        presence["key"]!.GetValue<string>().Should().Be("some-key");
        presence["enabled"]!.GetValue<bool>().Should().BeTrue();
    }

    [TestMethod]
    public void Register_ShouldIncludeEnabledInJoinPayload_GivenPresenceOptions()
    {
        var channel = Wire.Channel();
        channel.Register<PresenceExample>(PresenceOptions.WithPresence("some-key"));

        var presence = PresenceConfig(channel);
        presence["key"]!.GetValue<string>().Should().Be("some-key");
        presence["enabled"]!.GetValue<bool>().Should().BeTrue();
    }

    [TestMethod]
    public void Register_ShouldDisableEnabledInJoinPayload_GivenPresenceOptionsWithoutPresence()
    {
        var channel = Wire.Channel();
        channel.Register<PresenceExample>(PresenceOptions.WithoutPresence("some-key"));

        var presence = PresenceConfig(channel);
        presence["key"]!.GetValue<string>().Should().Be("some-key");
        presence["enabled"]!.GetValue<bool>().Should().BeFalse();
    }

    [TestMethod]
    public void GenerateJoinPush_ShouldOmitPresence_GivenNoRegistration()
    {
        var channel = Wire.Channel();
        var payload = channel.GenerateJoinPush().Payload!;
        var config = JsonNode.Parse(JsonSerializer.Serialize(payload, payload.GetType(), Wire.Settings()))!["config"]!
            .AsObject();

        config.ContainsKey("presence").Should().BeFalse();
    }
}
