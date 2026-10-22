using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Realtime.Tests.Support;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace Realtime.Tests.Channels;

/// <summary>
///     A subscribed channel's messaging behavior against the live stack: close notification on unsubscribe,
///     WALRUS array column delivery, join parameters on the wire, shared join state across duplicate topics,
///     and handler registration/removal.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ChannelMessagingTests
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
    public async Task Unsubscribe_ShouldRaiseClosedState()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.AddStateChangedHandler((_, state) =>
        {
            if (state == ChannelState.Closed)
                tsc.SetResult(true);
        });
        await channel.Subscribe();
        channel.Unsubscribe();
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task Unsubscribe_ShouldStopPostgresChanges()
    {
        var changedAfterLeave = false;
        var watcherSawInsert = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, _) => changedAfterLeave = true, ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });
        var watcher = this.socketClient.Channel("todos-watcher");
        watcher.OnPostgresChange((_, _) => watcherSawInsert.TrySetResult(true), ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });
        await channel.Subscribe();
        await watcher.Subscribe();
        channel.Unsubscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1 });
        await watcherSawInsert.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(500);
        Assert.IsFalse(changedAfterLeave, "the server must stop sending changes after Unsubscribe (issue #125)");
    }

    [TestMethod]
    public async Task Subscribe_ShouldDeliverArrayColumn_GivenInsert()
    {
        Todo? result = null;
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        var numbers = new List<int> { 4, 5, 6 };
        channel.OnPostgresChange((_, changes) =>
        {
            result = changes.Model<Todo>();
            tsc.SetResult(true);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Numbers = numbers });
        await tsc.Task;
        CollectionAssert.AreEqual(numbers, result?.Numbers);
    }

    [TestMethod]
    public async Task Subscribe_ShouldSendJoinParameters()
    {
        var parameters = new Dictionary<string, string> { { "key", "value" } };
        var channel = this.socketClient.Channel("realtime", "public", "todos", parameters: parameters);
        await channel.Subscribe();
        var payloadObj = channel.JoinPush?.Payload;
        var serialized = payloadObj is null ? "" : JsonSerializer.Serialize(payloadObj, payloadObj.GetType(), Wire.Settings());
        Assert.IsTrue(serialized.Contains("\"key\":\"value\""));
    }

    [TestMethod]
    public async Task Channel_ShouldShareJoinState_GivenDuplicateTopics()
    {
        var first = this.socketClient.Channel("realtime", "public", "todos");
        var second = this.socketClient.Channel("realtime", "public", "todos");
        var filtered = this.socketClient.Channel("realtime", "public", "todos", "user_id", "1");
        Assert.AreEqual(first.Topic, second.Topic);
        await first.Subscribe();
        Assert.AreEqual(first.HasJoinedOnce, second.HasJoinedOnce);
        Assert.AreNotEqual(first.HasJoinedOnce, filtered.HasJoinedOnce);
        var fourth = this.socketClient.Channel("realtime", "public", "todos");
        Assert.AreEqual(first.HasJoinedOnce, fourth.HasJoinedOnce);
    }

    [TestMethod]
    public async Task Channel_ShouldRegisterAndRemoveHandlers()
    {
        var channel = this.socketClient.Channel("test");
        IRealtimeChannel.StateChangedHandler stateHandler = (_, _) => Assert.Fail("State Handler was called");
        IRealtimeChannel.MessageReceivedHandler messageReceivedHandler =
            (_, _) => Assert.Fail("Message Handler was called");
        IRealtimeChannel.PostgresChangesHandler postgresChangesHandler =
            (_, _) => Assert.Fail("Postgres Changes Handler was called");
        channel.AddStateChangedHandler(stateHandler);
        channel.AddMessageReceivedHandler(messageReceivedHandler);
        channel.OnPostgresChange(postgresChangesHandler, ListenType.All, new PostgresChangesFilter { Table = "todos" });
        channel.Register<BroadcastExample>();
        channel.Register<PresenceExample>("user");
        channel.RemoveStateChangedHandler(stateHandler);
        channel.RemoveMessageReceivedHandler(messageReceivedHandler);
        channel.RemovePostgresChangeHandler(ListenType.All, postgresChangesHandler);
        await channel.Subscribe();
        await Task.Delay(500);
    }

    [TestMethod]
    public async Task Channel_ShouldDeliverToTheNewHandler_GivenResubscribeAfterUnsubscribe()
    {
        var oldHandlerFired = false;
        var newHandlerFired = new TaskCompletionSource<bool>();
        var first = this.socketClient.Channel("resubscribe");
        first.OnPostgresChange((_, _) => oldHandlerFired = true, ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });
        await first.Subscribe();
        first.Unsubscribe();
        var second = this.socketClient.Channel("resubscribe");
        second.OnPostgresChange((_, _) => newHandlerFired.TrySetResult(true), ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });
        await second.Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1 });
        await newHandlerFired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsFalse(oldHandlerFired, "the handler from before Unsubscribe must not fire");
    }

    [TestMethod]
    public async Task Channel_ShouldAllowPresenceAgain_GivenResubscribeAfterUnsubscribe()
    {
        var first = this.socketClient.Channel("presence-again");
        first.Register<PresenceExample>("user-1");
        await first.Subscribe();
        first.Unsubscribe();
        var second = this.socketClient.Channel("presence-again");
        second.Register<PresenceExample>("user-1");
        await second.Subscribe();
        Assert.IsTrue(second.IsJoined);
    }

    [TestMethod]
    public async Task Subscribe_ShouldStillDeliver_GivenTheSameInstanceAfterUnsubscribe()
    {
        var fired = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("same-instance");
        channel.OnPostgresChange((_, _) => fired.TrySetResult(true), ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });
        await channel.Subscribe();
        channel.Unsubscribe();
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1 });
        Assert.IsTrue(await fired.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }
}
