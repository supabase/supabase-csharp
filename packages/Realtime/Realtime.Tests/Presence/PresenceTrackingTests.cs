using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;

namespace Realtime.Tests.Presence;

/// <summary>
///     Presence against the live stack: two clients tracking presence on the same channel each observe the
///     other's state through the sync event.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class PresenceTrackingTests
{
    private IPostgrestClient restClient = null!;
    private IRealtimeClient<RealtimeSocket, RealtimeChannel> socketClient = null!;

    [TestInitialize]
    public async Task InitializeTest()
    {
        this.restClient = Helpers.RestClient();
        this.socketClient = Helpers.SocketClient();
        await this.socketClient.ConnectAsync();
    }

    [TestCleanup]
    public void CleanupTest() => this.socketClient.Disconnect();

    [TestMethod]
    public async Task Track_ShouldSyncPresenceBetweenClients()
    {
        var tsc = new TaskCompletionSource<bool>();
        var tsc2 = new TaskCompletionSource<bool>();
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var channel1 = this.socketClient.Channel("online-users");
        var presence1 = channel1.Register<PresenceExample>(guid1);
        presence1.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) =>
        {
            var state = presence1.CurrentState;
            if (state.ContainsKey(guid2) && state[guid2].First().Time != null)
                tsc.TrySetResult(true);
        });
        var client2 = Helpers.SocketClient();
        await client2.ConnectAsync();
        var channel2 = client2.Channel("online-users");
        var presence2 = channel2.Register<PresenceExample>(guid2);
        presence2.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) =>
        {
            var state = presence2.CurrentState;
            if (state.ContainsKey(guid1) && state[guid1].First().Time != null)
                tsc2.TrySetResult(true);
        });
        await channel1.Subscribe();
        await channel2.Subscribe();
        await presence1.Track(new PresenceExample { Time = DateTime.Now });
        await presence2.Track(new PresenceExample { Time = DateTime.Now });
        await presence1.Untrack();
        await Task.WhenAll(tsc.Task, tsc2.Task);
    }

    [TestMethod]
    public async Task CurrentState_ShouldHoldAnEarlierClient_GivenTheChannelJoinedLater()
    {
        var seen = new TaskCompletionSource();
        var earlier = Guid.NewGuid().ToString();
        var channel1 = this.socketClient.Channel("late-joiner");
        var presence1 = channel1.Register<PresenceExample>(earlier);
        await channel1.Subscribe();
        await presence1.Track(new PresenceExample());
        var client2 = Helpers.SocketClient();
        await client2.ConnectAsync();
        var channel2 = client2.Channel("late-joiner");
        var presence2 = channel2.Register<PresenceExample>(Guid.NewGuid().ToString());
        presence2.AddPresenceEventHandler(IRealtimePresence.EventType.Sync, (_, _) =>
        {
            if (presence2.CurrentState.ContainsKey(earlier))
                seen.TrySetResult();
        });
        await channel2.Subscribe();
        await seen.Awaiting(source => source.Task).Should().CompleteWithinAsync(TimeSpan.FromSeconds(10), "the snapshot sent on join lists who was already there");
        client2.Disconnect();
    }
}
