using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime.Socket;

namespace Realtime.Tests.Broadcast;

/// <summary>
///     Pins the exact bytes a <c>broadcast</c> frame writes to the socket. Unlike a join, the envelope carries
///     the broadcast event name in its <c>type</c> field, wrapping the caller's payload.
///     The envelope mirrors what <c>Push.Send</c> builds for a broadcast — the contract the System.Text.Json
///     migration must preserve.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class BroadcastFrameApprovalTests : FrameApprovalFixture
{
    [TestMethod]
    public async Task BroadcastFrame_ShouldSerializeToExpectedPayload_GivenAPayload()
    {
        var frame = new SocketRequest
        {
            Topic = "realtime:room:1",
            Type = "cursor-move",
            Event = "broadcast", // ChannelEventName.Broadcast maps to this wire value
            Payload = new Dictionary<string, object> { ["x"] = 12, ["y"] = 34 },
            Ref = "1",
            JoinRef = "1"
        };
        await this.Verify(this.Encode(frame)).UseDirectory("Data");
    }
}
