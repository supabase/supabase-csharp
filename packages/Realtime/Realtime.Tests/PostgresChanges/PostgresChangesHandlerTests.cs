using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace Realtime.Tests.PostgresChanges;

/// <summary>
///     Which postgres_changes handlers a channel invokes. Runs over a fake socket that replays the server's
///     join reply and change frames, so no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class PostgresChangesHandlerTests
{
    private const string Insert = """{"topic":"realtime:room","event":"postgres_changes","ref":null,"payload":{"data":{"schema":"public","table":"todos","type":"INSERT","record":{"id":1}},"ids":[7,8]}}""";

    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true };
    private RealtimeChannel channel = null!;

    [TestInitialize]
    public async Task Connect()
    {
        var client = new Client("ws://localhost/socket", new ClientOptions { WebSocketFactory = new FakeWebSocketFactory(this.socket) });
        await client.ConnectAsync();
        // Until the first heartbeat is out, the socket takes frames with a null ref for heartbeat replies.
        await this.socket.FirstSend.Task;
        this.channel = client.Channel("room");
    }

    [TestMethod]
    [DataRow(ListenType.Inserts, "INSERT")]
    [DataRow(ListenType.All, "*")]
    public async Task OnPostgresChange_ShouldInvokeEveryHandler_GivenTheSameFilter(ListenType listenType, string wireEvent)
    {
        var filter = new PostgresChangesFilter { Table = "todos" };
        int first = 0, second = 0;
        this.channel.OnPostgresChange((_, _) => first++, listenType, filter);
        this.channel.OnPostgresChange((_, _) => second++, listenType, filter);
        // The server answers one entry per listener; identical listeners share an id.
        var change = $$"""{"id":7,"event":"{{wireEvent}}","schema":"public","table":"todos"}""";
        await this.SubscribeAsync(change, change);
        this.socket.Receive(Insert);
        (first, second).Should().Be((1, 1));
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldInvokeTheEventHandler_GivenAWildcardHandlerOnTheSameTable()
    {
        var filter = new PostgresChangesFilter { Table = "todos" };
        int all = 0, inserts = 0;
        this.channel.OnPostgresChange((_, _) => all++, ListenType.All, filter);
        this.channel.OnPostgresChange((_, _) => inserts++, ListenType.Inserts, filter);
        await this.SubscribeAsync("""{"id":7,"event":"*","schema":"public","table":"todos"}""",
            """{"id":8,"event":"INSERT","schema":"public","table":"todos"}""");
        this.socket.Receive(Insert);
        (all, inserts).Should().Be((1, 1));
    }

    private async Task SubscribeAsync(params string[] changes)
    {
        var subscribe = this.channel.Subscribe();
        this.socket.Receive($$"""{"topic":"realtime:room","event":"phx_reply","ref":"{{this.channel.JoinPush!.Ref}}","payload":{"status":"ok","response":{"postgres_changes":[{{string.Join(",", changes)}}]""" + "}}}");
        this.socket.Receive("""{"topic":"realtime:room","event":"system","ref":null,"payload":{"extension":"postgres_changes","status":"ok"}}""");
        await subscribe;
    }
}
