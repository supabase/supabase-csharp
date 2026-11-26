using System;
using System.Threading;
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
///     The request <c>HttpSendAsync</c> and <c>HttpSendPersistedAsync</c> send and the errors they raise.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class HttpSendRequestTests
{
    private const string ApiKey = "publishable-key";
    private const int OneHourInSeconds = 3600;
    private const int UnprocessableEntity = 422;

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
        this.SentRequest().AbsoluteUrl.Should().EndWith("/realtime/v1/api/broadcast/room:1/events/cursor%2Fmove", "the server reads the topic without its realtime: prefix and the event as one escaped segment");
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendThePayloadAsJson()
    {
        await this.Channel().HttpSendAsync("cursor", new { x = 1 });
        var sent = this.SentRequest();
        using (new AssertionScope())
        {
            sent.Headers!["Content-Type"].Should().ContainSingle().Which.Should().StartWith("application/json", "the server decodes a JSON body only when told it is JSON");
            sent.Body.Should().Be("{\"x\":1}", "the payload is the message subscribers receive");
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendTheBytesAsOctetStream()
    {
        await this.Channel().HttpSendAsync("cursor", new byte[] { 1, 2, 3 });
        var sent = this.SentRequest();
        using (new AssertionScope())
        {
            sent.Headers!["Content-Type"].Should().ContainSingle().Which.Should().Be("application/octet-stream", "the server relays an octet stream body as binary");
            sent.BodyAsBytes.Should().Equal(new byte[] { 1, 2, 3 }, "the bytes must reach the server as given");
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldSendThePrivateFlag_GivenAPrivateChannel()
    {
        await this.Channel(options: Wire.PrivateOptions()).HttpSendAsync("cursor", new { x = 1 });
        this.SentRequest().RawQuery.Should().Be("?private=true", "the server authorizes a private broadcast only when the query says so");
    }

    [TestMethod]
    [DataRow(null, "?private=true&persist=true")]
    [DataRow(OneHourInSeconds, "?private=true&persist[ttl]=3600")]
    public async Task HttpSendPersistedAsync_ShouldAskToStoreTheMessageInTheQuery(int? ttlSeconds, string query)
    {
        await this.Channel(options: Wire.PrivateOptions()).HttpSendPersistedAsync("cursor", new { x = 1 }, Persistence(ttlSeconds));
        var sent = this.SentRequest();
        using (new AssertionScope())
        {
            sent.RawQuery.Should().Be(query, "the server reads persist only from the query string");
            sent.Body.Should().Be("""{"x":1}""", "a persist key in the body would be delivered as data");
        }
    }

    [TestMethod]
    public async Task HttpSendPersistedAsync_ShouldThrowAndSendNothing_GivenAPublicChannel()
    {
        var act = () => this.Channel().HttpSendPersistedAsync("cursor", new { x = 1 }, BroadcastPersistence.ForMaximumRetention);
        (await act.Should().ThrowAsync<RealtimeException>()).WithMessage("A persisted broadcast needs a private channel.", "the server stores a broadcast only on a private channel");
        this.server.LogEntries.Should().BeEmpty("the server answers 422 to persist on a public channel");
    }

    [TestMethod]
    public async Task HttpSendPersistedAsync_ShouldThrow_GivenNoPersistence()
    {
        var act = () => this.Channel(options: Wire.PrivateOptions()).HttpSendPersistedAsync("cursor", new { x = 1 }, null!);
        await act.Should().ThrowAsync<ArgumentNullException>("a persisted send needs to know how long the server keeps the broadcast");
    }

    [TestMethod]
    public async Task HttpSendPersistedAsync_ShouldThrowAndSendNothing_GivenACancelledToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var act = () => this.Channel(options: Wire.PrivateOptions()).HttpSendPersistedAsync("cursor", new { x = 1 }, BroadcastPersistence.ForMaximumRetention, cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("a caller that already gave up must not have the broadcast stored");
        this.server.LogEntries.Should().BeEmpty("nothing must be sent once the caller cancelled");
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
            headers["apiKey"].Should().Equal([ApiKey], "the client api key wins over a stale header");
            headers["Authorization"].Should().Equal(["Bearer user-jwt"], "the server authorizes the broadcast as the channel user");
            headers["X-Custom"].Should().Equal(["custom"], "the caller headers reach the server");
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldOmitTheAuthorization_GivenNoAccessToken()
    {
        await this.Channel().HttpSendAsync("cursor", new { x = 1 });
        this.SentRequest().Headers!.Should().NotContainKey("Authorization", "an empty bearer would be rejected instead of falling back to the api key");
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldThrowTheServerError_GivenARejection()
    {
        const string body = "{\"message\":\"Payload must be a JSON object\"}";
        this.server.ResetMappings();
        this.server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(UnprocessableEntity).WithBody(body));
        var act = () => this.Channel().HttpSendAsync("cursor", 1);
        var exception = (await act.Should().ThrowAsync<RealtimeException>("a rejection surfaces as the SDK exception")).Which;
        using (new AssertionScope())
        {
            exception.Message.Should().Be(body, "the caller must see why the server rejected the broadcast");
            exception.StatusCode.Should().Be(UnprocessableEntity, "the caller must see the status the server answered");
        }
    }

    [TestMethod]
    public async Task HttpSendAsync_ShouldThrow_GivenNoAnswerWithinTheClientTimeout()
    {
        this.server.ResetMappings();
        this.server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(202).WithDelay(TimeSpan.FromSeconds(5)));
        this.clientOptions.Timeout = TimeSpan.FromMilliseconds(200);
        var act = () => this.Channel().HttpSendAsync("cursor", new { x = 1 });
        await act.Should().ThrowAsync<OperationCanceledException>("the client timeout bounds how long a send waits");
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
        await act.Should().ThrowAsync<OperationCanceledException>("the client timeout runs on the supplied clock").WaitAsync(TimeSpan.FromSeconds(5));
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
        BlockingCaller.Completes(() => this.Channel().HttpSendAsync("cursor", new { x = 1 })).Should().BeTrue("the SDK must not resume on the caller's synchronization context");

    private static BroadcastPersistence Persistence(int? ttlSeconds) =>
        ttlSeconds is { } seconds ? BroadcastPersistence.For(TimeSpan.FromSeconds(seconds)) : BroadcastPersistence.ForMaximumRetention;

    private RealtimeChannel Channel(string topic = "realtime:room", ChannelOptions? options = null) =>
        new(new RealtimeSocket(this.SocketUrl, this.clientOptions), topic, options ?? Wire.PublicOptions());

    private string SocketUrl => $"{this.server.Url!.Replace("http", "ws")}/realtime/v1";

    private IRequestMessage SentRequest() => this.server.LogEntries.Should().ContainSingle("a send posts exactly one request").Which.RequestMessage!;
}
