using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;

namespace Realtime.Tests.Serialization;

/// <summary>
///     The <c>config</c> block a channel sends when it joins: whether it is flagged private, which
///     postgres_changes listeners it carries, and that absent broadcast/presence sections are omitted rather
///     than sent as null.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class JoinPushConfigTests
{
    private static JsonObject Config(JoinPush joinPush) =>
        JsonNode.Parse(JsonSerializer.Serialize(joinPush))!["config"]!.AsObject();

    [TestMethod]
    public void ForPrivateChannel_ShouldFlagConfigPrivate() => Config(JoinPush.ForPrivateChannel())["private"]!.GetValue<bool>().Should().BeTrue();

    [TestMethod]
    public void ForPublicChannel_ShouldNotFlagConfigPrivate() => Config(JoinPush.ForPublicChannel())["private"]!.GetValue<bool>().Should().BeFalse();

    [TestMethod]
    public void ForPublicChannel_ShouldOmitBroadcastAndPresence_GivenAbsent()
    {
        var config = Config(JoinPush.ForPublicChannel());
        config.ContainsKey("broadcast").Should().BeFalse();
        config.ContainsKey("presence").Should().BeFalse();
    }

    [TestMethod]
    public void ForPublicChannel_ShouldCarryPostgresChangesListeners()
    {
        var options = new List<PostgresChangesOptions> { new("public", "todos") };
        var config = Config(JoinPush.ForPublicChannel(postgresChangesOptions: options));
        config["postgres_changes"]!.AsArray().Count.Should().Be(1);
        config["postgres_changes"]![0]!["table"]!.GetValue<string>().Should().Be("todos");
    }

    [TestMethod]
    public void ForPublicChannel_ShouldSerialiseBroadcast_GivenProvided()
    {
        var config = Config(JoinPush.ForPublicChannel(new BroadcastOptions(broadcastSelf: true, broadcastAck: true)));
        config["broadcast"]!["self"]!.GetValue<bool>().Should().BeTrue();
        config["broadcast"]!["ack"]!.GetValue<bool>().Should().BeTrue();
    }

    [TestMethod]
    public void ForPublicChannel_ShouldSerialisePresenceKey_GivenProvided()
    {
        var config = Config(JoinPush.ForPublicChannel(presenceOptions: PresenceOptions.WithPresence("client-1")));
        config["presence"]!["key"]!.GetValue<string>().Should().Be("client-1");
        config["presence"]!["enabled"]!.GetValue<bool>().Should().BeTrue();
    }
}
