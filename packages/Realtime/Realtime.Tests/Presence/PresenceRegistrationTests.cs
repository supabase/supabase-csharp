using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;
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

    private static bool EnabledAfterSubscribe(Action<RealtimePresence<PresenceExample>> listen)
    {
        var channel = Wire.Channel();
        listen(channel.Register<PresenceExample>(PresenceOptions.WithoutPresence("some-key")));
        _ = channel.Subscribe();
        return PresenceConfig(channel)["enabled"]!.GetValue<bool>();
    }

    private static async Task<RealtimePresence<PresenceExample>> TrackOnAChannelJoinedWithoutPresence(string reply)
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true, AnswerJoins = true };
        var client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(socket) });
        await client.ConnectAsync();
        var channel = client.Channel("room");
        var presence = channel.Register<PresenceExample>(PresenceOptions.WithoutPresence("some-key"));
        await channel.Subscribe();
        var tracking = presence.Track(new PresenceExample());
        socket.Reply(channel.Topic, channel.LastPush!.Ref, reply);
        await tracking.WaitAsync(TimeSpan.FromSeconds(5));
        return presence;
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
    public void Subscribe_ShouldTurnPresenceOn_GivenASyncHandlerAddedBeforeIt() =>
        EnabledAfterSubscribe(presence => presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => { }))
            .Should().BeTrue("the server sends presence only to a channel that joined with it on, so a handler must turn it on");

    [TestMethod]
    public void Subscribe_ShouldTurnPresenceOn_GivenAJoinHandlerAddedBeforeIt() =>
        EnabledAfterSubscribe(presence => presence.AddJoinHandler(_ => { }))
            .Should().BeTrue("a join handler must turn presence on like any other presence handler");

    [TestMethod]
    public void Subscribe_ShouldTurnPresenceOn_GivenALeaveHandlerAddedBeforeIt() =>
        EnabledAfterSubscribe(presence => presence.AddLeaveHandler(_ => { }))
            .Should().BeTrue("a leave handler must turn presence on like any other presence handler");

    [TestMethod]
    public void AddJoinHandler_ShouldThrow_GivenASubscribedChannelWithPresenceOff()
    {
        var channel = Wire.Channel();
        var presence = channel.Register<PresenceExample>(PresenceOptions.WithoutPresence("some-key"));
        _ = channel.Subscribe();
        presence.Invoking(late => late.AddJoinHandler(_ => { })).Should().Throw<RealtimeException>()
            .WithMessage("Cannot add presence handlers for realtime:example after `Subscribe()` while presence is off.")
            .Where(e => e.Reason == FailureHint.Reason.StateInvalid, "the join already told the server presence is off, so the handler would never be called");
    }

    [TestMethod]
    public void AddJoinHandler_ShouldNotThrow_GivenASubscribedChannelWithPresenceOn()
    {
        var channel = Wire.Channel();
        var presence = channel.Register<PresenceExample>("some-key");
        _ = channel.Subscribe();
        presence.Invoking(late => late.AddJoinHandler(_ => { })).Should().NotThrow("a channel that joined with presence on gets presence events, so a late handler must be accepted");
    }

    [TestMethod]
    public async Task AddJoinHandler_ShouldNotThrow_GivenATrackTurnedPresenceOn()
    {
        var presence = await TrackOnAChannelJoinedWithoutPresence(Constants.PhoenixStatusOk);
        presence.Invoking(late => late.AddJoinHandler(_ => { })).Should().NotThrow("a track that succeeds turns presence on for a channel that joined with it off");
    }

    [TestMethod]
    public async Task AddJoinHandler_ShouldThrow_GivenATrackTheServerRefused()
    {
        var presence = await TrackOnAChannelJoinedWithoutPresence(Constants.PhoenixStatusError);
        presence.Invoking(late => late.AddJoinHandler(_ => { })).Should().Throw<RealtimeException>("a refused track leaves presence off, so the handler must not be accepted");
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
