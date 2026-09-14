using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Channels;

/// <summary>
///     Pins the exact bytes of the <c>phx_join</c> frame a channel writes to the socket when it subscribes.
///     The frame's <c>config</c> block is the richest serialization surface in Realtime — nested
///     broadcast/presence sections, the postgres_changes listener array with its <c>event</c> enum mapped to
///     the wire string, and per-field null omission — so it is the contract the System.Text.Json migration
///     must preserve. The envelope mirrors what <c>Push.Send</c> builds for a join.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SubscribeFrameApprovalTests : FrameApprovalFixture
{
    private static SocketRequest JoinFrame(string topic, JoinPush payload) =>
        new() { Topic = topic, Event = ChannelEventJoin, Payload = payload, Ref = "1", JoinRef = "1" };

    [TestMethod]
    public async Task JoinFrame_ShouldSerializeToExpectedPayload_GivenPostgresChangesListeners()
    {
        var payload = JoinPush.ForPublicChannel(postgresChangesOptions: new List<PostgresChangesOptions>
        {
            new("public", "todos", PostgresChangesOptions.ListenType.Inserts),
            new("public", "messages", PostgresChangesOptions.ListenType.All, "id=eq.1")
        });
        await this.Verify(this.Encode(JoinFrame("realtime:public:todos", payload))).UseDirectory("Data");
    }

    [TestMethod]
    public async Task JoinFrame_ShouldFlagPrivate_GivenPrivateChannel()
    {
        await this.Verify(this.Encode(JoinFrame("realtime:private:todos", JoinPush.ForPrivateChannel())))
            .UseDirectory("Data");
    }

    [TestMethod]
    public async Task JoinFrame_ShouldSerializeBroadcastAndPresence_GivenOptions()
    {
        var payload = JoinPush.ForPublicChannel(
            new BroadcastOptions(broadcastSelf: true, broadcastAck: true),
            PresenceOptions.WithPresence("client-1"));
        await this.Verify(this.Encode(JoinFrame("realtime:room:1", payload))).UseDirectory("Data");
    }
}
