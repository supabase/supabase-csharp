using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Websocket.Client;
using static Supabase.Realtime.Constants;

namespace Realtime.Tests.Connection;

/// <summary>
///     After a failed reconnect attempt the client waits <see cref="ClientOptions.ReconnectAfterInterval" /> and
///     tries again itself. Runs over a fake socket, so no server is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ReconnectDelayTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AfterTheDelay = TimeSpan.FromMilliseconds(300);

    private readonly FakeWebsocketClient socket = new() { OpenOnStart = true, ErrorReconnectTimeout = TimeSpan.FromSeconds(1) };
    private readonly ClientOptions options = new();
    private readonly List<int> attempts = [];
    private TimeSpan delay = TimeSpan.FromMilliseconds(20);
    private Client client = null!;

    [TestInitialize]
    public void CreateClient()
    {
        this.options.WebSocketFactory = new FakeWebSocketFactory(this.socket);
        this.options.ReconnectAfterInterval = attempt =>
        {
            this.attempts.Add(attempt);
            return this.delay;
        };
        this.client = new Client("ws://localhost/socket", this.options);
    }

    [TestCleanup]
    public void Disconnect() => this.client.Disconnect();

    [TestMethod]
    public async Task ReconnectAfterInterval_ShouldDelayTheNextAttempt_GivenAFailedAttempt()
    {
        this.delay = TimeSpan.FromMilliseconds(300);
        await this.client.ConnectAsync();
        var waited = Stopwatch.StartNew();
        this.FailAnAttempt();
        await this.socket.Reconnected.Task.WaitAsync(Patience);
        waited.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(200), "the client must wait the delay before it tries again");
    }

    [TestMethod]
    public async Task ReconnectAfterInterval_ShouldGetTheAttemptNumber_GivenFailuresBeforeAndAfterTheSocketOpened()
    {
        await this.client.ConnectAsync();
        this.FailAnAttempt();
        this.FailAnAttempt();
        this.socket.IsRunning = true;
        this.socket.RecoverFromLostConnection();
        this.FailAnAttempt();
        this.attempts.Should().Equal([1, 2, 1], "the attempt number must grow with each failed attempt and start again once a socket has opened");
    }

    [TestMethod]
    public async Task ReconnectAfterInterval_ShouldForgetTheWait_GivenTheSocketOpenedAndDroppedAgainBeforeItEnded()
    {
        await this.client.ConnectAsync();
        this.FailAnAttempt();
        this.socket.IsRunning = true;
        this.socket.RecoverFromLostConnection();
        this.socket.IsRunning = false;
        await this.socket.Awaiting(s => s.Reconnected.Task).Should().NotCompleteWithinAsync(AfterTheDelay, "a wait from an earlier outage must not retry during the next one");
    }

    [TestMethod]
    public async Task ReconnectAfterInterval_ShouldNotBeAsked_GivenALostConnection()
    {
        await this.client.ConnectAsync();
        this.socket.Fail(new InvalidOperationException("reset"), DisconnectionType.Lost);
        this.attempts.Should().BeEmpty("the transport retries a lost connection at once, so the client must not schedule an attempt on top of it");
    }

    [TestMethod]
    public async Task ReconnectAfterInterval_ShouldReconnectAtOnce_GivenANegativeDelay()
    {
        this.delay = TimeSpan.FromSeconds(-5);
        await this.client.ConnectAsync();
        this.FailAnAttempt();
        await this.socket.Awaiting(s => s.Reconnected.Task).Should().CompleteWithinAsync(Patience, "a negative delay must count as no delay");
    }

    [TestMethod]
    public async Task ReconnectAfterInterval_ShouldNotReconnect_GivenTheSocketIsAlreadyBack()
    {
        await this.client.ConnectAsync();
        this.FailAnAttempt();
        this.socket.IsRunning = true;
        await this.socket.Awaiting(s => s.Reconnected.Task).Should().NotCompleteWithinAsync(AfterTheDelay, "reconnecting an open socket would drop a working connection");
    }

    [TestMethod]
    public async Task Disconnect_ShouldStopTheReconnect_GivenAStateHandlerCallsItOnClose()
    {
        this.client.AddStateChangedHandler((_, state) =>
        {
            if (state == SocketState.Close)
                this.client.Disconnect();
        });
        await this.client.ConnectAsync();
        this.FailAnAttempt();
        await this.socket.Awaiting(s => s.Reconnected.Task).Should().NotCompleteWithinAsync(AfterTheDelay, "a client the app disconnected must not reconnect");
    }

    [TestMethod]
    public async Task ConnectAsync_ShouldTurnOffTheTransportRetryTimer()
    {
        await this.client.ConnectAsync();
        this.socket.ErrorReconnectTimeout.Should().BeNull("the client waits between attempts itself, so the transport must not retry on its own timer");
    }

    [TestMethod]
    [DataRow(10000, 15000)]
    [DataRow(0, null)]
    [DataRow(-1, null)]
    public async Task ConnectAsync_ShouldSetTheSilenceWatchdogFromTheHeartbeat(int heartbeatMilliseconds, int? watchdogMilliseconds)
    {
        this.options.HeartbeatInterval = TimeSpan.FromMilliseconds(heartbeatMilliseconds);
        await this.client.ConnectAsync();
        (this.socket.ReconnectTimeout?.TotalMilliseconds).Should().Be(watchdogMilliseconds, "the no-message watchdog must follow the heartbeat interval and be off without one");
    }

    [TestMethod]
    [DataRow(3, 4)]
    [DataRow(6, 30)]
    public void ReconnectAfterInterval_ShouldBeARandomDelayUpToTheCurve_GivenTheDefault(int attempt, int longestSeconds)
    {
        var defaults = new ClientOptions();
        var longest = Enumerable.Range(0, 1000).Max(_ => defaults.ReconnectAfterInterval(attempt).TotalSeconds);
        longest.Should().BeInRange(longestSeconds / 2.0, longestSeconds, "the default delay must be random up to 1 second doubled per attempt, capped at 30");
    }

    private void FailAnAttempt()
    {
        this.socket.IsRunning = false;
        this.socket.Fail(new InvalidOperationException("refused"));
    }
}
