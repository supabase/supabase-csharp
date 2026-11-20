using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Gotrue.Tests.Observability;

/// <summary>
/// What the Auth client tells a logger: each request's outcome at Debug, and the failures of work that runs in the background
/// (auto-refresh, session persistence, state change handlers) at Warning (client.observability.diagnostic_logging).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GotrueLoggingTests
{
    private static readonly TimeSpan RefreshDeadline = TimeSpan.FromSeconds(10);

    private WireMockServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = WireMockServer.Start();

    [TestCleanup]
    public void TestCleanup() => this.server.Stop();

    private Client ClientWith(ILogger logger, bool autoRefresh = false) =>
        new(new ClientOptions { Url = this.server.Url!, AutoRefreshToken = autoRefresh, Logger = logger });

    private static Session ExpiredSession() =>
        new() { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 1, CreatedAt = DateTime.UtcNow.AddHours(-1) };

    private static Supabase.Gotrue.Interfaces.IGotrueSessionPersistence<Session> PersistenceOf(Session? session)
    {
        var persistence = SessionPersistenceSubstitute.Tracking();
        if (session != null)
            persistence.SaveSession(session);
        return persistence;
    }

    [TestMethod]
    public async Task Settings_ShouldLogTheRequestOutcomeAtDebug()
    {
        this.server.Given(Request.Create().WithPath("/settings").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
        var capture = new LogCapture();
        await this.ClientWith(capture.Logger).Settings();
        capture.Records.Should().ContainSingle().Which.Message.Should().StartWith($"GET {this.server.Url}/settings responded 200 in ");
    }

    [TestMethod]
    public void LoadSession_ShouldLogAWarning_GivenThePersistenceThrows()
    {
        var capture = new LogCapture();
        var client = this.ClientWith(capture.Logger);
        var persistence = Substitute.For<Supabase.Gotrue.Interfaces.IGotrueSessionPersistence<Session>>();
        persistence.LoadSession().Returns(_ => throw new InvalidOperationException("storage unavailable: secret-detail"));
        client.SetPersistence(persistence);
        client.LoadSession();
        capture.Records.Should().ContainSingle(record => record.Level == LogLevel.Warning).Which.Message.Should()
            .Be("Supabase.Gotrue: Session load failed with InvalidOperationException", "the failure has no caller to return an error to");
    }

    [TestMethod]
    public async Task LoadSessionAsync_ShouldLogAWarning_GivenThePersistenceThrows()
    {
        var capture = new LogCapture();
        var client = this.ClientWith(capture.Logger);
        var persistence = Substitute.For<Supabase.Gotrue.Interfaces.IGotrueSessionPersistence<Session>>();
        persistence.LoadSessionAsync(Arg.Any<CancellationToken>()).Returns<Task<Session?>>(_ => throw new InvalidOperationException("storage unavailable"));
        client.SetPersistence(persistence);
        await client.LoadSessionAsync();
        capture.Records.Should().ContainSingle(record => record.Level == LogLevel.Warning).Which.Message.Should()
            .Be("Supabase.Gotrue: Session load failed with InvalidOperationException");
    }

    [TestMethod]
    public void LoadSession_ShouldLogAWarning_GivenAStateChangeHandlerThrows()
    {
        var capture = new LogCapture();
        var client = this.ClientWith(capture.Logger);
        client.SetPersistence(PersistenceOf(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 }));
        client.AddStateChangedListener((_, _) => throw new InvalidOperationException("handler bug"));
        client.LoadSession();
        capture.Records.Should().ContainSingle(record => record.Level == LogLevel.Warning).Which.Message.Should()
            .Be("Supabase.Gotrue: Auth state change handler failed with InvalidOperationException");
    }

    [TestMethod]
    public async Task AutoRefresh_ShouldLogAWarning_GivenTheBackgroundRefreshFails()
    {
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));
        var capture = new LogCapture();
        var client = this.ClientWith(capture.Logger, autoRefresh: true);
        client.SetPersistence(PersistenceOf(ExpiredSession()));
        client.LoadSession();
        await WaitUntil(() => capture.Records.Any(record => record.Level == LogLevel.Warning));
        capture.Records.First(record => record.Level == LogLevel.Warning).Message.Should().Be("Supabase.Gotrue: Token refresh failed with GotrueException",
            "a refresh on the timer has no caller to return an error to");
    }

    [TestMethod]
    public async Task AutoRefresh_ShouldLogTheTimerLifecycleAtDebug()
    {
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));
        var capture = new LogCapture();
        var client = this.ClientWith(capture.Logger, autoRefresh: true);
        client.SetPersistence(PersistenceOf(ExpiredSession()));
        client.LoadSession();
        await WaitUntil(() => capture.Records.Any(record => record.Message == "Supabase.Gotrue: refresh timer scheduled"));
        client.StopAutoRefresh();
        capture.Records.Should().Contain(record => record.Level == LogLevel.Debug && record.Message == "Supabase.Gotrue: refresh timer scheduled");
    }

    [TestMethod]
    public async Task SignOut_ShouldLogTheTimerStopAtDebug()
    {
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(204));
        var capture = new LogCapture();
        var client = this.ClientWith(capture.Logger, autoRefresh: true);
        client.SetPersistence(PersistenceOf(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600, CreatedAt = DateTime.UtcNow }));
        client.LoadSession();
        await client.SignOut();
        capture.Records.Should().Contain(record => record.Message == "Supabase.Gotrue: refresh timer stopped");
    }

    [TestMethod]
    public async Task SignOut_ShouldLogAWarning_GivenThePersistenceHandlerThrows()
    {
        this.server.Given(Request.Create().UsingAnyMethod()).RespondWith(Response.Create().WithStatusCode(204));
        var capture = new LogCapture();
        var client = this.ClientWith(capture.Logger);
        var persistence = SessionPersistenceSubstitute.Tracking();
        persistence.DestroySessionAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("persistence bug"));
        persistence.SaveSession(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600, CreatedAt = DateTime.UtcNow });
        client.SetPersistence(persistence);
        client.LoadSession();
        await client.SignOut();
        capture.Records.Should().Contain(record => record.Level == LogLevel.Warning && record.Message == "Supabase.Gotrue: Session persistence handler failed with InvalidOperationException");
    }

    [TestMethod]
    public void AutoRefresh_ShouldLogAWarning_GivenTheRefreshTimerCannotBeArmed()
    {
        var capture = new LogCapture();
        var client = new Client(new ClientOptions { Url = this.server.Url!, AutoRefreshToken = true, MaximumRefreshWaitTime = int.MaxValue, Logger = capture.Logger });
        client.SetPersistence(PersistenceOf(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 4_000_000_000, CreatedAt = DateTime.UtcNow }));
        client.LoadSession();
        capture.Records.Should().Contain(record => record.Level == LogLevel.Warning && record.Message == "Supabase.Gotrue: Refresh timer initialization failed with ArgumentOutOfRangeException");
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + RefreshDeadline;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }
}
