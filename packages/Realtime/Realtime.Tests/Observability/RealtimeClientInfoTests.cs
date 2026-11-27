using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Core;
using Supabase.Realtime;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Realtime.Tests.Observability;

/// <summary>
/// The WebSocket handshake and the HTTP broadcast identify the SDK in <c>X-Client-Info</c>, or carry the value the
/// application supplied in any casing, without ever a <c>features</c> parameter (client.observability.client_info).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RealtimeClientInfoTests
{
    private const string SocketUrl = "ws://localhost:4000/socket";
    private const string SuppliedValue = "my-lib/1.0";

    private static readonly string SdkValue = Util.GetAssemblyVersion(typeof(Client));

    private readonly FakeWebSocketFactory factory = new(new FakeWebsocketClient());

    [TestMethod]
    public void Handshake_ShouldCarryTheSdkValue()
    {
        _ = new RealtimeSocket(SocketUrl, this.Options());
        this.factory.HeadersProvider!().Should().Equal(new Dictionary<string, string> { ["X-Client-Info"] = SdkValue });
    }

    [TestMethod]
    public void Handshake_ShouldCarryTheSuppliedValue_GivenOptionHeaderInAnyCasing()
    {
        var options = this.Options();
        options.Headers["x-client-info"] = SuppliedValue;
        _ = new RealtimeSocket(SocketUrl, options);
        this.factory.HeadersProvider!().Should().Equal(new Dictionary<string, string> { ["x-client-info"] = SuppliedValue },
            "an application-supplied value replaces the SDK's, in a single header");
    }

    [TestMethod]
    public void Handshake_ShouldCarryTheSuppliedValue_GivenDynamicHeaders()
    {
        _ = new RealtimeSocket(SocketUrl, this.Options()) { GetHeaders = () => new Dictionary<string, string> { ["X-Client-Info"] = SuppliedValue } };
        this.factory.HeadersProvider!().Should().Equal(new Dictionary<string, string> { ["X-Client-Info"] = SuppliedValue },
            "through the umbrella client, the umbrella identifies the SDK");
    }

    [TestMethod]
    public void Constructor_ShouldLeaveTheOptionHeadersUntouched()
    {
        var options = this.Options();
        _ = new RealtimeSocket(SocketUrl, options);
        this.factory.HeadersProvider!();
        options.Headers.Should().BeEmpty("the application owns its options");
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendTheSdkValue()
    {
        var server = WireMockServer.Start();
        try
        {
            server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(202));
            var socket = new RealtimeSocket($"{server.Url!.Replace("http", "ws")}/realtime/v1", new ClientOptions());
            await new RealtimeChannel(socket, "realtime:room", Wire.PublicOptions()).HttpSendAsync("cursor", new { x = 1 });
            server.LogEntries.Should().ContainSingle().Which.RequestMessage!.Headers!["X-Client-Info"].Should().Equal(SdkValue);
        }
        finally
        {
            server.Stop();
        }
    }

    private ClientOptions Options() => new() { WebSocketFactory = this.factory };
}
