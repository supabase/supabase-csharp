using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;

namespace Realtime.Tests.Connection;

/// <summary>
///     The client asks <see cref="ClientOptions.RetrieveAccessToken" /> for the token on connect and on every heartbeat.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class AccessTokenCallbackTests
{
    private Client client = null!;

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public async Task ConnectAsync_ShouldUseTheCallbackToken()
    {
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true }, () => Task.FromResult<string?>("jwt"));
        await this.client.ConnectAsync();
        this.CurrentToken().Should().Be("jwt");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldKeepTheCurrentToken_GivenTheCallbackReturnsNull()
    {
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true }, () => Task.FromResult<string?>(null));
        this.client.SetAuth("current");
        await this.client.ConnectAsync();
        this.CurrentToken().Should().Be("current");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldThrowTheCallbackFailure_GivenTheCallbackThrows()
    {
        var failure = new InvalidOperationException("boom");
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true }, () => throw failure);
        var connecting = () => this.client.ConnectAsync();
        (await connecting.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure,
            "a token that cannot be resolved must fail the connect rather than open the socket with a stale or lower-privilege token (client.authentication_integration.third_party_auth)");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldThrow_GivenDisconnectWhileTheCallbackIsPending()
    {
        var token = new TaskCompletionSource<string?>();
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true }, () => token.Task);
        var connecting = this.client.ConnectAsync();
        this.client.Disconnect();
        token.SetResult("jwt");
        var act = () => connecting.WaitAsync(TimeSpan.FromSeconds(5));
        (await act.Should().ThrowAsync<RealtimeException>().WithMessage("Disconnected before the socket opened.")).Which.Reason.Should().Be(FailureHint.Reason.SocketError,
            "a Disconnect during the token lookup must not leave a socket that opens afterwards");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldConnect_GivenTheIdleDelayOfAnEarlierDisconnectPassesWhileTheCallbackIsPending()
    {
        var token = new TaskCompletionSource<string?>();
        var calls = 0;
        this.client = new Client("ws://localhost/socket", new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(new FakeWebsocketClient { OpenOnStart = true }),
            DisconnectOnEmptyChannelsAfter = TimeSpan.FromMilliseconds(50),
            RetrieveAccessToken = () => ++calls == 1 ? Task.FromResult<string?>("first") : token.Task,
        });
        await this.client.ConnectAsync();
        this.client.Channel("room");
        this.client.Disconnect();
        var connecting = this.client.ConnectAsync();
        await Task.Delay(200);
        token.SetResult("second");
        await connecting.Awaiting(connect => connect).Should().NotThrowAsync("the idle delay of a connection that is already closed must not end the next connect");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldKeepTheCurrentTokenAndLogTheFailure_GivenTheCallbackThrows()
    {
        var failure = new InvalidOperationException("boom");
        var calls = 0;
        var logged = new TaskCompletionSource<Exception>();
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true },
            () => ++calls == 1 ? Task.FromResult<string?>("first") : throw failure);
        await this.client.ConnectAsync();
        this.client.AddDebugHandler((_, _, error) => _ = error is null || logged.TrySetResult(error));
        (await logged.Task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeSameAs(failure, "a heartbeat has no caller to throw to, so its failure is logged");
        this.client.ClearDebugHandlers();
        this.CurrentToken().Should().Be("first", "a failed heartbeat refresh keeps the token the socket already holds");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldUseTheCallbackToken()
    {
        var calls = 0;
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true },
            () => Task.FromResult<string?>(++calls == 1 ? "first" : "second"));
        await this.client.ConnectAsync();
        await this.NextHeartbeatReply();
        this.CurrentToken().Should().Be("second");
    }

    [TestMethod]
    public async Task Heartbeat_ShouldResendTheStoredToken_GivenNoCallback()
    {
        var socket = new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true, AnswerJoins = true };
        var afterJoin = socket.SentFrame("access_token", 1);
        var afterHeartbeat = socket.SentFrame("access_token", 2);
        this.client = ClientOver(socket, null);
        this.client.SetAuth("stored");
        await this.client.ConnectAsync();
        await this.client.Channel("room").Subscribe();
        await afterJoin.WaitAsync(TimeSpan.FromSeconds(5));
        var resent = await afterHeartbeat.WaitAsync(TimeSpan.FromSeconds(5));
        ((string?) resent["payload"]!["access_token"]).Should().Be("stored", "every heartbeat re-sends the client's token to the channels it has joined");
    }

    [TestMethod]
    public void ConnectAsync_ShouldComplete_GivenCallerBlockingOnContextThatNeverRunsContinuations()
    {
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true }, () => Task.Run<string?>(() => "jwt"));
        BlockingCaller.Completes(() => this.client.ConnectAsync()).Should().BeTrue();
    }

    private static Client ClientOver(FakeWebsocketClient socket, Func<Task<string?>>? retrieveAccessToken) =>
        new("ws://localhost/socket", new ClientOptions
        {
            WebSocketFactory = new FakeWebSocketFactory(socket),
            HeartbeatInterval = TimeSpan.FromMilliseconds(50),
            RetrieveAccessToken = retrieveAccessToken,
        });

    private string? CurrentToken() => this.client.Channel("room").Options.RetrieveAccessToken();

    private Task NextHeartbeatReply()
    {
        var answered = new TaskCompletionSource();
        this.client.Socket!.AddHeartbeatHandler((_, _) => answered.TrySetResult());
        return answered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
