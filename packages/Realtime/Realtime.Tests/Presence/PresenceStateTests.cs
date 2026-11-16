using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Presence;
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

    private const string TrackEchoingNullRefsFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s6\"," +
        "\"payload\":{\"joins\":{\"user-2\":{\"metas\":[{\"phx_ref\":\"r1\",\"phx_ref\":null,\"phx_ref_prev\":null}]}}," +
        "\"leaves\":{}}}";

    private const string RetrackEchoingNullRefsFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s7\"," +
        "\"payload\":{\"joins\":{\"user-2\":{\"metas\":[{\"phx_ref\":\"r2\",\"phx_ref_prev\":\"r1\",\"phx_ref\":null,\"phx_ref_prev\":null}]}}," +
        "\"leaves\":{\"user-2\":{\"metas\":[{\"phx_ref\":\"r1\",\"phx_ref\":null,\"phx_ref_prev\":null}]}}}}";

    private const string StateFrameWithoutPayload =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_state\",\"ref\":\"s7\",\"payload\":null}";

    private const string StateFrameAfterReconnect =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_state\",\"ref\":\"s5\"," +
        "\"payload\":{\"user-2\":{\"metas\":[{\"phx_ref\":\"r2\"}]}}}";

    /// <summary>
    /// Track while already joined: the server joins the new meta (<c>phx_ref_prev</c> = the old ref)
    /// and leaves the previous <c>phx_ref</c> in the same diff. The key stays present.
    /// </summary>
    private const string TrackWhileJoinedFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s6\"," +
        "\"payload\":{\"joins\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r2\",\"phx_ref_prev\":\"r1\",\"time\":\"2023-09-01T11:00:00Z\"}]}}," +
        "\"leaves\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\",\"time\":\"2023-09-01T10:00:00Z\"}]}}}}";

    /// <summary>
    /// Two users present, then user-1 re-tracks while user-3 joins and user-2 leaves in the same diff.
    /// </summary>
    private const string TwoUsersStateFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_state\",\"ref\":\"s7\"," +
        "\"payload\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\"}]},\"user-2\":{\"metas\":[{\"phx_ref\":\"q1\"}]}}}";

    private const string TrackAndJoinFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s8\"," +
        "\"payload\":{\"joins\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r2\",\"phx_ref_prev\":\"r1\"}]},\"user-3\":{\"metas\":[{\"phx_ref\":\"t1\"}]}}," +
        "\"leaves\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\"}]}}}}";

    private const string TrackAndLeaveFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s9\"," +
        "\"payload\":{\"joins\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r2\",\"phx_ref_prev\":\"r1\"}]}}," +
        "\"leaves\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\"}]},\"user-2\":{\"metas\":[{\"phx_ref\":\"q1\"}]}}}}";

    /// <summary>
    /// The same key joins and leaves, but the leave removes every meta: the user is gone, so both events fire.
    /// </summary>
    private const string JoinAndLeaveAllFrame =
        "{\"topic\":\"realtime:online-users\",\"event\":\"presence_diff\",\"ref\":\"s10\"," +
        "\"payload\":{\"joins\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r2\"}]}}," +
        "\"leaves\":{\"user-1\":{\"metas\":[{\"phx_ref\":\"r1\"},{\"phx_ref\":\"r2\"}]}}}}";

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

    private static (int joins, int leaves) Replay(string stateFrame, string diffFrame)
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(stateFrame));
        int joins = 0, leaves = 0;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Join, (_, _) => joins++);
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Leave, (_, _) => leaves++);
        channel.HandleSocketMessage(Wire.Decode(diffFrame));
        return (joins, leaves);
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldStillRaiseJoin_GivenTrackOfOneUserAndJoinOfAnother()
    {
        var (joins, leaves) = Replay(TwoUsersStateFrame, TrackAndJoinFrame);
        using (new AssertionScope())
        {
            joins.Should().Be(1, "user-3 really joined");
            leaves.Should().Be(0, "user-1 only replaced its meta");
        }
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldStillRaiseLeave_GivenTrackOfOneUserAndLeaveOfAnother()
    {
        var (joins, leaves) = Replay(TwoUsersStateFrame, TrackAndLeaveFrame);
        using (new AssertionScope())
        {
            joins.Should().Be(0, "user-1 only replaced its meta");
            leaves.Should().Be(1, "user-2 really left");
        }
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldRaiseJoinAndLeave_GivenKeyThatEndsAbsent()
    {
        var (joins, leaves) = Replay(StateFrame, JoinAndLeaveAllFrame);
        using (new AssertionScope())
        {
            joins.Should().Be(1);
            leaves.Should().Be(1);
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
    public void HandleSocketMessage_ShouldKeepTheNewMeta_GivenARetrackEchoingNullRefs()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(TrackEchoingNullRefsFrame));
        channel.HandleSocketMessage(Wire.Decode(RetrackEchoingNullRefsFrame));
        presence.CurrentState.Should().ContainKey("user-2", "8.0 to 8.10 clients sent null refs and the server echoes them after its own")
            .WhoseValue.Should().ContainSingle().Which.PhoenixRef.Should().Be("r2");
    }

    [TestMethod]
    public void Serialize_ShouldOmitPhoenixRefs_GivenAModelTakenFromCurrentState()
    {
        var model = new PresenceExample { PhoenixRef = "r2", PhoenixPrevRef = "r1" };
        JsonSerializer.SerializeToNode<object>(model, Wire.Settings())!.AsObject().Select(property => property.Key)
            .Should().Equal(["time"], "the server assigns the refs and echoes any the client sends after its own");
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
    public void ClearPresenceEventHandlers_ShouldKeepTheOtherHandlers_GivenAnEventTypeWithNone()
    {
        var (channel, presence) = Presence();
        var syncs = 0;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => syncs++);
        presence.ClearPresenceEventHandlers(IRealtimePresence.EventType.Join);
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        syncs.Should().Be(1, "clearing the join handlers must not remove the sync handler");
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldRaiseJoinThenLeaveThenSync_GivenPresenceDiff()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        var events = new List<string>();
        presence.AddJoinHandler(join => events.Add(
            $"join {join.Key} current={join.CurrentPresences.Count} new={join.NewPresences.Single().PhoenixRef} state={presence.CurrentState.Keys.Single()}"));
        presence.AddLeaveHandler(leave => events.Add(
            $"leave {leave.Key} current={leave.CurrentPresences.Count} left={leave.LeftPresences.Single().PhoenixRef} time={leave.LeftPresences.Single().Time != null}"));
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => events.Add("sync"));
        channel.HandleSocketMessage(Wire.Decode(DiffFrame));
        events.Should().Equal(["join user-2 current=0 new=r2 state=user-1", "leave user-1 current=0 left=r1 time=True", "sync"],
            "the handlers must get the presences the state held and run before it changes");
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldRaiseJoinAndLeaveForWhatChanged_GivenASecondPresenceState()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        var events = new List<string>();
        presence.AddJoinHandler(join => events.Add($"join {join.Key}"));
        presence.AddLeaveHandler(leave => events.Add($"leave {leave.Key}"));
        channel.HandleSocketMessage(Wire.Decode(StateFrameAfterReconnect));
        events.Should().Equal(["join user-2", "leave user-1"], "a second presence_state must report only what changed while the channel was away");
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldReportTheOtherConnection_GivenTwoConnectionsOnOneKey()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        var joins = new List<PresenceJoin<PresenceExample>>();
        var leaves = new List<PresenceLeave<PresenceExample>>();
        presence.AddJoinHandler(joins.Add);
        presence.AddLeaveHandler(leaves.Add);
        channel.HandleSocketMessage(Wire.Decode(SecondConnectionJoinFrame));
        channel.HandleSocketMessage(Wire.Decode(FirstConnectionLeaveFrame));
        using (new AssertionScope())
        {
            joins.Should().ContainSingle().Which.CurrentPresences.Should().ContainSingle().Which.PhoenixRef.Should().Be("r1", "a join must carry the presences the key already had");
            leaves.Should().ContainSingle().Which.CurrentPresences.Should().ContainSingle().Which.PhoenixRef.Should().Be("r2", "a leave must carry the presences the key still has");
        }
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldRaiseNoJoinOrLeave_GivenAPresenceStateThatChangesNothing()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        var changes = 0;
        presence.AddJoinHandler(_ => changes++);
        presence.AddLeaveHandler(_ => changes++);
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        changes.Should().Be(0, "a presence_state that repeats the state must not report anyone as joined or left");
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldKeepState_GivenPresenceStateWithoutPayload()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        channel.HandleSocketMessage(Wire.Decode(StateFrameWithoutPayload));
        presence.CurrentState.Should().ContainKey("user-1", "a presence_state without a payload must not clear the state");
    }

    [TestMethod]
    public void HandleSocketMessage_ShouldStillApplyTheChange_GivenAThrowingJoinHandler()
    {
        var (channel, presence) = Presence();
        var syncs = 0;
        presence.AddJoinHandler(_ => throw new InvalidOperationException());
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => syncs++);
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        using (new AssertionScope())
        {
            presence.CurrentState.Should().ContainKey("user-1", "a throwing handler must not stop the state from changing");
            syncs.Should().Be(1, "a throwing handler must not stop the others");
        }
    }

    [TestMethod]
    [DataRow(null, 0, 0)]
    [DataRow(IRealtimePresence.EventType.Join, 0, 1)]
    [DataRow(IRealtimePresence.EventType.Leave, 1, 0)]
    public void ClearPresenceEventHandlers_ShouldDropTheTypedHandlers_GivenTheirEventTypeOrNone(IRealtimePresence.EventType? cleared, int expectedJoins, int expectedLeaves)
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        int joins = 0, leaves = 0;
        presence.AddJoinHandler(_ => joins++);
        presence.AddLeaveHandler(_ => leaves++);
        presence.ClearPresenceEventHandlers(cleared);
        channel.HandleSocketMessage(Wire.Decode(DiffFrame));
        using (new AssertionScope())
        {
            joins.Should().Be(expectedJoins, "clearing must drop the join handlers only for Join or for every event type");
            leaves.Should().Be(expectedLeaves, "clearing must drop the leave handlers only for Leave or for every event type");
        }
    }

    [TestMethod]
    public void Unsubscribe_ShouldRaiseLeaveForEveryKeyThenSync()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(TwoUsersStateFrame));
        var events = new List<string>();
        presence.AddLeaveHandler(leave => events.Add($"leave {leave.Key} current={leave.CurrentPresences.Count} left={leave.LeftPresences.Single().PhoenixRef}"));
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => events.Add($"sync state={presence.CurrentState.Count}"));
        channel.Unsubscribe();
        events.Should().Equal(["leave user-1 current=0 left=r1", "leave user-2 current=0 left=q1", "sync state=0"],
            "unsubscribing ends the presence state, so everyone it held must be reported as left");
    }

    [TestMethod]
    public void RemoveJoinHandler_ShouldStopTheCalls_GivenRemoveLeaveHandlerToo()
    {
        var (channel, presence) = Presence();
        var calls = 0;
        Action<PresenceJoin<PresenceExample>> join = _ => calls++;
        Action<PresenceLeave<PresenceExample>> leave = _ => calls++;
        presence.AddJoinHandler(join);
        presence.AddLeaveHandler(leave);
        presence.RemoveJoinHandler(join);
        presence.RemoveLeaveHandler(leave);
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        channel.HandleSocketMessage(Wire.Decode(DiffFrame));
        calls.Should().Be(0, "a removed handler must not be called again");
    }

    [TestMethod]
    public void Unsubscribe_ShouldRaiseNoSync_GivenAnEmptyState()
    {
        var (channel, presence) = Presence();
        var syncs = 0;
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => syncs++);
        channel.Unsubscribe();
        syncs.Should().Be(0, "a channel that held no presence must not raise a sync when it closes");
    }

    [TestMethod]
    public void Unsubscribe_ShouldNotThrow_GivenAThrowingSyncHandler()
    {
        var (channel, presence) = Presence();
        channel.HandleSocketMessage(Wire.Decode(StateFrame));
        presence.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) => throw new InvalidOperationException());
        channel.Invoking(leaving => leaving.Unsubscribe()).Should().NotThrow("a handler that throws must not stop the client from unsubscribing its other channels");
    }

    [TestMethod]
    public void TriggerDiff_ShouldThrow_GivenUnparsableResponse()
    {
        var (_, presence) = Presence();
        var act = () => presence.TriggerDiff(new SocketResponse(Wire.Settings()));
        act.Should().Throw<ArgumentException>();
    }
}
