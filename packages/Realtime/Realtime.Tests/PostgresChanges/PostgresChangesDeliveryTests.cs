using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Models;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace Realtime.Tests.PostgresChanges;

/// <summary>
///     Postgres change delivery against the live stack: a channel that subscribed with an
///     <c>OnPostgresChange</c> listener receives the matching insert/update/delete callbacks (filtered and
///     wildcard), models the payload, and fans out to multiple listeners.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class PostgresChangesDeliveryTests
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
    public async Task OnPostgresChange_ShouldModelPayload()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("example");
        channel.OnPostgresChange((_, changes) =>
        {
            var model = changes.Model<Todo>();
            tsc.SetResult(model != null);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "*" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client Models a response? ✅" });
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldThrowError_GivenOnChangeAfterSubscribe()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("example");
        channel.OnPostgresChange((_, changes) =>
        {
            var model = changes.Model<Todo>();
            tsc.SetResult(model != null);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "*" });

        await channel.Subscribe();

        var act = () => channel.OnPostgresChange((_, changes) =>
        {
            var model = changes.Model<Todo>();
            tsc.SetResult(model != null);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "*" });

        Assert.Throws<RealtimeException>(act);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldThrowError_GivenRegisterChangesAfterSubscribe()
    {
        var channel = this.socketClient.Channel("example");
        await channel.Subscribe();

        var act = () => channel.RegisterPostgresChangesOptions(new PostgresChangesOptions("example"));
        Assert.Throws<RealtimeException>(act);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldReceiveInsert()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, _) => tsc.SetResult(true), ListenType.Inserts,
            new PostgresChangesFilter { Table = "todos" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldReceiveFilteredInsert()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, changes) =>
        {
            Assert.AreEqual("Client receives filtered insert callback? ✅", changes.Model<Todo>()?.Details);
            tsc.SetResult(true);
        }, ListenType.Inserts,
            new PostgresChangesFilter { Table = "todos", Filter = "details=eq.Client receives filtered insert callback? ✅" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 2, Details = "Client receives filtered insert callback? ✅" });
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldReceiveUpdateAndFilteredInsert()
    {
        var tsc = new TaskCompletionSource<bool>();
        var response = await this.restClient.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });
        await this.restClient.Table<Todo>()
            .Insert(new Todo { UserId = 2, Details = "Client receives filtered insert callback? ✅" });
        var model = response.Models.First();
        var oldDetails = model.Details;
        var newDetails = $"I'm an updated item ✏️ - {DateTime.Now}";
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, changes) =>
        {
            Assert.AreEqual(oldDetails, changes.OldModel<Todo>()?.Details);
            var updated = changes.Model<Todo>();
            Assert.AreEqual(newDetails, updated?.Details);
            if (updated != null)
            {
                Assert.AreEqual(model.Id, updated.Id);
                Assert.AreEqual(model.UserId, updated.UserId);
            }
            tsc.SetResult(true);
        }, ListenType.Updates, new PostgresChangesFilter { Table = "todos" });
        const string filter = "Client receives filtered insert callback? ✅";
        channel.OnPostgresChange((_, changes) =>
        {
            Assert.AreEqual("Client receives filtered insert callback? ✅", changes.Model<Todo>()?.Details);
            tsc.SetResult(true);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{filter}" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Set(x => x.Details!, newDetails).Match(model).Update();
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldReceiveUpdate()
    {
        var tsc = new TaskCompletionSource<bool>();
        var response = await this.restClient.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });
        var model = response.Models.First();
        var oldDetails = model.Details;
        var newDetails = $"I'm an updated item ✏️ - {DateTime.Now}";
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, changes) =>
        {
            Assert.AreEqual(oldDetails, changes.OldModel<Todo>()?.Details);
            var updated = changes.Model<Todo>();
            Assert.AreEqual(newDetails, updated?.Details);
            if (updated != null)
            {
                Assert.AreEqual(model.Id, updated.Id);
                Assert.AreEqual(model.UserId, updated.UserId);
            }
            tsc.SetResult(true);
        }, ListenType.Updates, new PostgresChangesFilter { Table = "todos" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Set(x => x.Details!, newDetails).Match(model).Update();
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldReceiveDelete()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, _) => tsc.SetResult(true), ListenType.Deletes,
            new PostgresChangesFilter { Table = "todos" });
        await channel.Subscribe();
        var result = await this.restClient.Table<Todo>().Get();
        var model = result.Models.Last();
        await this.restClient.Table<Todo>().Match(model).Delete();
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldReceiveFilteredDelete()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        var todo1 = await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives callbacks 1? ✅" });
        var todo2 = await this.restClient.Table<Todo>().Insert(new Todo { UserId = 2, Details = "Client receives callbacks 2? ✅" });
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 3, Details = "Client receives callbacks 3? ✅" });
        channel.OnPostgresChange((_, removed) =>
        {
            var result = removed.OldModel<Todo>();
            Assert.AreEqual(result?.Details, todo1.Model?.Details);
            Assert.AreNotEqual(result?.Details, todo2.Model?.Details);
            tsc.SetResult(true);
        }, ListenType.Deletes,
            new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{todo1.Model?.Details}" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Match(todo1.Models.First()).Delete();
        await this.restClient.Table<Todo>().Match(todo2.Models.First()).Delete();
        Assert.IsTrue(await tsc.Task);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldReceiveAllEvents_GivenWildcard()
    {
        var insertTsc = new TaskCompletionSource<bool>();
        var updateTsc = new TaskCompletionSource<bool>();
        var deleteTsc = new TaskCompletionSource<bool>();
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, changes) =>
        {
            switch (changes.Payload?.Data?.Type)
            {
                case EventType.Insert: insertTsc.SetResult(true); break;
                case EventType.Update: updateTsc.SetResult(true); break;
                case EventType.Delete: deleteTsc.SetResult(true); break;
            }
        }, ListenType.All, new PostgresChangesFilter { Table = "todos" });
        await channel.Subscribe();
        var inserted = await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives wildcard callbacks? ✅" });
        var newModel = inserted.Models.First();
        await this.restClient.Table<Todo>().Set(x => x.Details!, "And edits.").Match(newModel).Update();
        await this.restClient.Table<Todo>().Match(newModel).Delete();
        await Task.WhenAll(insertTsc.Task, updateTsc.Task, deleteTsc.Task);
        Assert.IsTrue(insertTsc.Task.Result);
        Assert.IsTrue(updateTsc.Task.Result);
        Assert.IsTrue(deleteTsc.Task.Result);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldFanOutToMultipleInsertListeners()
    {
        var insertTask1 = new TaskCompletionSource<bool>();
        var insertTask2 = new TaskCompletionSource<bool>();
        var insertTask3 = new TaskCompletionSource<bool>();
        const string filter1 = "Client receives callbacks 1? ✅";
        const string filter2 = "Client receives callbacks 2? ✅";
        var channel = this.socketClient.Channel("realtime", "public", "todos");
        var count = 0;
        channel.OnPostgresChange((_, _) =>
        {
            count++;
            if (count == 3) insertTask1.TrySetResult(true);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });
        channel.OnPostgresChange((_, added) =>
            insertTask2.SetResult(added.Model<Todo>()?.Details == filter1), ListenType.Inserts,
            new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{filter1}" });
        channel.OnPostgresChange((_, added) =>
            insertTask3.SetResult(added.Model<Todo>()?.Details == filter2), ListenType.Inserts,
            new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{filter2}" });
        await channel.Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives wildcard callbacks? ✅" });
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = filter1 });
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = filter2 });
        await Task.WhenAll(insertTask1.Task, insertTask2.Task, insertTask3.Task);
        Assert.IsTrue(insertTask1.Task.Result);
        Assert.IsTrue(insertTask2.Task.Result);
        Assert.IsTrue(insertTask3.Task.Result);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldRegisterAndDeliver_GivenChainedSubscribe()
    {
        var tsc = new TaskCompletionSource<bool>();
        await this.socketClient.Channel("public:todos")
            .OnPostgresChange((_, changes) => tsc.TrySetResult(changes.Model<Todo>() != null),
                ListenType.Inserts, new PostgresChangesFilter { Table = "todos" })
            .Subscribe();
        await this.restClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "OnPostgresChange receives insert? ✅" });
        Assert.IsTrue(await WithinTimeout(tsc.Task));
    }

    private static async Task<bool> WithinTimeout(Task<bool> task, int timeoutMs = 15000)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        return completed == task && task.Result;
    }
}
