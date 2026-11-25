using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Realtime.Tests.HttpSend;

/// <summary>
///     The request <c>HttpSendAsync</c> sends and the errors it raises.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class HttpSendRequestTests
{
    private const string ApiKey = "publishable-key";

    private WireMockServer server = null!;
    private ClientOptions clientOptions = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = WireMockServer.Start();
        this.server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(202));
        this.clientOptions = new ClientOptions { Parameters = { ApiKey = ApiKey } };
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    [TestMethod]
    [DataRow("realtime:room:1")]
    [DataRow("room:1")]
    public async Task HttpSendAsync_ShouldPostToTheEventUrl(string topic)
    {
        await this.Channel(topic).HttpSendAsync("cursor/move", new { x = 1 });
        this.SentRequest().AbsoluteUrl.Should().EndWith("/realtime/v1/api/broadcast/room:1/events/cursor%2Fmove");
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendThePayloadAsJson()
    {
        await this.Channel().HttpSendAsync("cursor", new { x = 1 });
        var sent = this.SentRequest();
        using (new AssertionScope())
        {
            sent.Headers!["Content-Type"].Should().ContainSingle().Which.Should().StartWith("application/json");
            sent.Body.Should().Be("{\"x\":1}");
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendTheBytesAsOctetStream()
    {
        await this.Channel().HttpSendAsync("cursor", new byte[] { 1, 2, 3 });
        var sent = this.SentRequest();
        using (new AssertionScope())
        {
            sent.Headers!["Content-Type"].Should().ContainSingle().Which.Should().Be("application/octet-stream");
            sent.BodyAsBytes.Should().Equal(1, 2, 3);
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendThePrivateFlag_GivenAPrivateChannel()
    {
        await this.Channel(options: Wire.PrivateOptions()).HttpSendAsync("cursor", new { x = 1 });
        this.SentRequest().RawQuery.Should().Be("?private=true");
    }

    [TestMethod]
    [DataRow(null, "?private=true&persist=true")]
    [DataRow(3600, "?private=true&persist[ttl]=3600")]
    [DataRow(0, "?private=true&persist[ttl]=0")]
    public async Task HttpSendAsync_ShouldAskToStoreTheMessageInTheQuery_GivenAPersistence(int? ttlSeconds, string query)
    {
        await this.Channel(options: Wire.PrivateOptions()).HttpSendAsync("cursor", new { x = 1 }, new BroadcastPersistence(ttlSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null));
        var sent = this.SentRequest();
        using (new AssertionScope())
        {
            sent.RawQuery.Should().Be(query, "the server reads persist only from the query string, and owns the range of the ttl");
            sent.Body.Should().Be("""{"x":1}""", "a persist key in the body would be delivered as data");
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldThrowAndSendNothing_GivenAPersistenceOnAPublicChannel()
    {
        var act = () => this.Channel().HttpSendAsync("cursor", new { x = 1 }, new BroadcastPersistence());
        (await act.Should().ThrowAsync<RealtimeException>()).WithMessage("A persisted broadcast needs a private channel.");
        this.server.LogEntries.Should().BeEmpty("the server answers 422 to persist on a public channel");
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldThrow_GivenNoPersistence()
    {
        var act = () => this.Channel(options: Wire.PrivateOptions()).HttpSendAsync("cursor", new { x = 1 }, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendTheClientHeaders()
    {
        this.clientOptions.Headers["apiKey"] = "stale-key";
        this.clientOptions.Headers["X-Custom"] = "custom";
        await this.Channel(options: Wire.PublicOptions("user-jwt")).HttpSendAsync("cursor", new { x = 1 });
        var headers = this.SentRequest().Headers!;
        using (new AssertionScope())
        {
            headers["apiKey"].Should().Equal(ApiKey);
            headers["Authorization"].Should().Equal("Bearer user-jwt");
            headers["X-Custom"].Should().Equal("custom");
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldOmitTheAuthorization_GivenNoAccessToken()
    {
        await this.Channel().HttpSendAsync("cursor", new { x = 1 });
        this.SentRequest().Headers!.Should().NotContainKey("Authorization");
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldThrowTheServerError_GivenARejection()
    {
        const string body = "{\"message\":\"Payload must be a JSON object\"}";
        this.server.ResetMappings();
        this.server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(422).WithBody(body));
        var act = () => this.Channel().HttpSendAsync("cursor", 1);
        var exception = (await act.Should().ThrowAsync<RealtimeException>()).Which;
        using (new AssertionScope())
        {
            exception.Message.Should().Be(body);
            exception.StatusCode.Should().Be(422);
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldThrow_GivenNoAnswerWithinTheClientTimeout()
    {
        this.server.ResetMappings();
        this.server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(202).WithDelay(TimeSpan.FromSeconds(5)));
        this.clientOptions.Timeout = TimeSpan.FromMilliseconds(200);
        var act = () => this.Channel().HttpSendAsync("cursor", new { x = 1 });
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldThrow_GivenTheSuppliedClockReachedTheClientTimeout()
    {
        this.server.ResetMappings();
        this.server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(202).WithDelay(TimeSpan.FromMinutes(1)));
        var clock = new FakeTimeProvider();
        this.clientOptions.Timeout = TimeSpan.FromHours(1);
        this.clientOptions.TimeProvider = clock;
        var send = this.Channel().HttpSendAsync("cursor", new { x = 1 });
        clock.Advance(TimeSpan.FromHours(1));
        var act = () => send;
        await act.Should().ThrowAsync<OperationCanceledException>().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendTheClientHeaders_GivenAChannelCreatedWhileDisconnected()
    {
        var client = new Client(this.SocketUrl, this.clientOptions) { GetHeaders = () => new() { ["X-Tenant"] = "acme" } };
        await client.Channel("room").HttpSendAsync("cursor", new { x = 1 });
        this.SentRequest().Headers!["X-Tenant"].Should().ContainSingle().Which.Should().Be("acme", "a channel must be able to send over HTTP without a socket connection");
    }

    [TestMethod]
    public void HttpSendAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations() =>
        BlockingCaller.Completes(() => this.Channel().HttpSendAsync("cursor", new { x = 1 })).Should().BeTrue();

    private RealtimeChannel Channel(string topic = "realtime:room", ChannelOptions? options = null) =>
        new(new RealtimeSocket(this.SocketUrl, this.clientOptions), topic, options ?? Wire.PublicOptions());

    private string SocketUrl => $"{this.server.Url!.Replace("http", "ws")}/realtime/v1";

    private IRequestMessage SentRequest() => this.server.LogEntries.Should().ContainSingle().Which.RequestMessage!;
}
