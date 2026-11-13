using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;

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
    public async Task ConnectAsync_ShouldLogTheFailure_GivenTheCallbackThrows()
    {
        var failure = new InvalidOperationException("boom");
        var logged = new List<Exception?>();
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true }, () => throw failure);
        this.client.AddDebugHandler((_, _, error) => logged.Add(error));
        await this.client.ConnectAsync();
        this.client.ClearDebugHandlers();
        logged.Should().Contain(failure);
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
        this.client = ClientOver(new FakeWebsocketClient { OpenOnStart = true, AnswerHeartbeats = true }, null);
        this.client.SetAuth("stored");
        await this.client.ConnectAsync();
        var channel = this.client.Channel("room");
        await this.NextHeartbeatReply();
        channel.Options.Parameters.Should().Contain("user_token", "stored");
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
