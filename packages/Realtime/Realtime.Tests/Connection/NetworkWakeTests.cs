using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Realtime.Tests.Support;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;
using Websocket.Client;

namespace Realtime.Tests.Connection;

/// <summary>
///     While the socket waits to reconnect, the platform's report that the network is back ends the wait early
///     (realtime.configuration.network_path_wake). Runs over a fake socket and a fake network monitor, so no server
///     and no network change is needed.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class NetworkWakeTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly FakeWebsocketClient connection = new() { OpenOnStart = true };
    private readonly ClientOptions options = new();
    private TimeSpan delay = TimeSpan.FromHours(1);
    private SocketOverAFakeMonitor socket = null!;

    [TestInitialize]
    public void CreateSocket()
    {
        this.options.WebSocketFactory = new FakeWebSocketFactory(this.connection);
        this.options.ReconnectAfterInterval = _ => this.delay;
        this.socket = new SocketOverAFakeMonitor(this.options);
    }

    [TestCleanup]
    public void Disconnect() => this.socket.Disconnect();

    [TestMethod]
    public async Task NetworkReport_ShouldEndTheWaitTwoSecondsAfterTheAttemptStarted_GivenItComesSooner()
    {
        await this.socket.Connect();
        var sinceTheAttemptStarted = Stopwatch.StartNew();
        this.DropTheConnection();
        this.FailTheAttempt();
        this.socket.ReportNetworkBack();
        await this.connection.Reconnected.Task.WaitAsync(Patience);
        sinceTheAttemptStarted.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.8), "a network that comes back must end the wait, but no sooner than 2 seconds after the previous attempt started");
    }

    [TestMethod]
    public async Task NetworkReport_ShouldStartOneAttemptAtOnce_GivenThePreviousAttemptStartedMoreThanTwoSecondsAgo()
    {
        await this.socket.Connect();
        this.DropTheConnection();
        await Task.Delay(TimeSpan.FromSeconds(2.2));
        this.FailTheAttempt();
        this.socket.ReportNetworkBack();
        await this.connection.Reconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));
        this.socket.ReportNetworkBack();
        this.FailTheAttempt();
        this.socket.ReportNetworkBack();
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        this.connection.ReconnectCallCount.Should().Be(1, "a report must not interrupt the attempt in flight, and the 2 seconds count again from the start of that attempt");
    }

    [TestMethod]
    public async Task NetworkReport_ShouldNotLengthenTheWait_GivenTheDelayEndsBeforeTheTwoSeconds()
    {
        this.delay = TimeSpan.FromMilliseconds(300);
        await this.socket.Connect();
        this.DropTheConnection();
        this.FailTheAttempt();
        this.socket.ReportNetworkBack();
        await this.connection.Awaiting(c => c.Reconnected.Task).Should().CompleteWithinAsync(TimeSpan.FromSeconds(1.5), "a network that comes back must never make the socket wait longer than its delay");
    }

    [TestMethod]
    public async Task Connect_ShouldNotWatchTheNetwork_GivenMonitorsNetworkPathIsOff()
    {
        this.options.MonitorsNetworkPath = false;
        await this.socket.Connect();
        this.socket.Watching.Should().BeFalse("the option turns the network monitor off");
    }

    [TestMethod]
    public async Task Connect_ShouldSucceed_GivenThePlatformHasNoNetworkMonitor()
    {
        this.socket = new SocketOverAFakeMonitor(this.options) { PlatformHasNoMonitor = true };
        await this.socket.Awaiting(s => s.Connect()).Should().NotThrowAsync("a runtime without a network monitor has no wake, and must still connect");
    }

    [TestMethod]
    public async Task Connect_ShouldStopWatchingTheNetwork_GivenTheSocketWasDisposedWhileItOpened()
    {
        this.socket.AddStateChangedHandler((sender, _) => ((IDisposable) sender).Dispose());
        await Assert.ThrowsAsync<RealtimeException>(() => this.socket.Connect());
        this.socket.Watching.Should().BeFalse("a socket disposed while it opened must not keep the network monitor");
    }

    [TestMethod]
    public async Task Disconnect_ShouldStopWatchingTheNetwork()
    {
        await this.socket.Connect();
        this.socket.Disconnect();
        this.socket.Watching.Should().BeFalse("a disconnected socket must release the network monitor");
    }

    [TestMethod]
    public async Task Dispose_ShouldStopWatchingTheNetwork()
    {
        await this.socket.Connect();
        ((IDisposable) this.socket).Dispose();
        this.socket.Watching.Should().BeFalse("a disposed socket must release the network monitor");
    }

    private void DropTheConnection()
    {
        this.connection.IsRunning = false;
        this.connection.Fail(new InvalidOperationException("dropped"), DisconnectionType.Lost);
    }

    private void FailTheAttempt() => this.connection.Fail(new InvalidOperationException("refused"));

    private sealed class SocketOverAFakeMonitor(ClientOptions options) : RealtimeSocket("ws://localhost/socket", options)
    {
        public Action ReportNetworkBack { get; private set; } = () => { };

        public bool Watching { get; private set; }

        public bool PlatformHasNoMonitor { get; init; }

        internal override Action WatchNetworkPath(Action networkIsBack)
        {
            if (this.PlatformHasNoMonitor)
                throw new PlatformNotSupportedException();
            this.ReportNetworkBack = networkIsBack;
            this.Watching = true;
            return () => this.Watching = false;
        }
    }
}
