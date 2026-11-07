using System;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;

namespace Realtime.Tests.Presence;

/// <summary>
///     How a channel folds presence frames into shared state: a <c>presence_state</c> frame seeds the current
///     state, and a <c>presence_diff</c> applies joins and leaves. Both are driven through the channel over a
///     fake socket.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PresenceStateTests
{
    private const string StateFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_state\",\"ref\":\"s1\"," +
        "\"payload\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\",\"time\":\"2023-09-01T10:00:00Z\"}]}}}";

    private const string DiffFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s2\"," +
        "\"payload\":{\"joins\":{\"user-2\":{\"metas\":[{\"phx_ref\":\"r2\",\"time\":\"2023-09-01T11:00:00Z\"}]}}," +
        "\"leaves\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\"}]}}}}";

    private const string SecondConnectionJoinFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s3\"," +
        "\"payload\":{\"joins\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r2\"}]}},\"leaves\":{}}}";

    private const string FirstConnectionLeaveFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s4\"," +
        "\"payload\":{\"joins\":{},\"leaves\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\"}]}}}}";

    private const string StateFrameAfterReconnect =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_state\",\"ref\":\"s5\"," +
        "\"payload\":{\"user-2\":{\"metas\":[{\"phx_ref\":\"r2\"}]}}}";

    // Track while already joined: the server joins the new meta (phx_ref_prev = the old ref)
    // and leaves the previous phx_ref in the same diff. The key stays present.
    private const string TrackWhileJoinedFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s6\"," +
        "\"payload\":{\"joins\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r2\",\"phx_ref_prev\":\"r1\",\"time\":\"2023-09-01T11:00:00Z\"}]}}," +
        "\"leaves\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\",\"time\":\"2023-09-01T10:00:00Z\"}]}}}}";

    private static (RealtimeChannel channel, RealtimePresence<PresenceExample> presence) Presence()
    {
        var channel = Wire.Channel("realtime:online-users");
        return (channel, channel.Register<PresenceExample>("user-1"));
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldSeedStateAndRaiseSync_GivenPresenceState()
    {
        var (channel, presence) = Presence();
        var syncs = 0;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => syncs++);
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        using (new AssertionScope())
        {
            presence.CurrentState.Should().ContainKey("user-1");
            presence.CurrentState["user-1"].Should().ContainSingle().Which.Time.Should().NotBeNull();
            syncs.Should().Be(1);
        }
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldApplyJoinsAndLeaves_GivenPresenceDiff()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        int joins = 0, leaves = 0;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Join, (_, _) => joins++);
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Leave, (_, _) => leaves++);
        channel.HandleSocketMessage(Wire.Decode(DiffFrame));
        using (new AssertionScope())
        {
            presence.CurrentState.Should().ContainKey("user-2");
            presence.CurrentState.Should().NotContainKey("user-1");
            joins.Should().Be(1);
            leaves.Should().Be(1);
        }
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldSyncOnly_GivenTrackOfAnAlreadyPresentUser()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        int syncs = 0, joins = 0, leaves = 0;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => syncs++);
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Join, (_, _) => joins++);
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Leave, (_, _) => leaves++);

        channel.HandleSocketMessage(Wire.Decode(TrackWhileJoinedFrame));

        using (new AssertionScope())
        {
            presence.CurrentState.Should().ContainKey("user-1");
            presence.CurrentState["user-1"].Should().ContainSingle()
                .Which.PhoenixRef.Should().Be("r2", "the new meta replaces the previous phx_ref");
            syncs.Should().Be(1);
            joins.Should().Be(0, "the user was already present; the join is the replacement meta");
            leaves.Should().Be(0, "the leave is the previous phx_ref and the user is still present");
        }
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldTrackEachMeta_GivenTwoConnectionsOnOneKey()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        channel.HandleSocketMessage(Wire.Decode(SecondConnectionJoinFrame));
        presence.CurrentState["user-1"].Should().HaveCount(2, "a second connection joined the same key");

        channel.HandleSocketMessage(Wire.Decode(FirstConnectionLeaveFrame));
        presence.CurrentState.Should().ContainKey("user-1", "the user is still online on the other connection")
            .WhoseValue.Should().ContainSingle().Which.PhoenixRef.Should().Be("r2");
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldReplaceState_GivenASecondPresenceState()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        channel.HandleSocketMessage(Wire.Decode(StateFrameAfterReconnect));
        presence.CurrentState.Keys.Should().Equal(["user-2"], "user-1 left during the drop, so no leave diff will ever remove them");
    }

    [TestMethod]
    public void TriggerDiff_ShouldThrow_GivenUnparsableResponse()
    {
        var (_, presence) = Presence();
        var act = () => presence.TriggerDiff(new SocketResponse(Wire.Settings()));
        act.Should().Throw<ArgumentException>();
    }
}
