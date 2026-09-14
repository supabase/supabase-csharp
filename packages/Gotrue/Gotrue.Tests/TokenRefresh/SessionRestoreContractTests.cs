#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.TokenRefresh;

/// <summary>
///     Pins how a restored session survives startup: only a server-rejected refresh token signs the
///     user out - a network error or an offline client keeps the persisted session (issue #390).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SessionRestoreContractTests
{
    /// <summary>Bounds every wait on a handler, so a bug fails the test in seconds instead of hanging it.</summary>
    private static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Every client the test built, shut down centrally so a failed assertion cannot leak its refresh timer.</summary>
    private readonly List<IGotrueClient<User, Session>> clients = new();

    private IGotrueSessionPersistence<Session> persistence = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.persistence = SessionPersistenceSubstitute.Tracking();
        this.persistence.SaveSession(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
    }

    [TestCleanup]
    public void TestCleanup()
    {
        try
        {
            foreach (var client in this.clients)
            {
                client.Shutdown();
            }
        }
        finally
        {
            this.server.Dispose();
        }
    }

    [TestMethod]
    public async Task RetrieveSessionAsync_ShouldKeepTheSession_GivenTheRefreshFailsWithANetworkError()
    {
        var client = this.Restore(TestClients.Against(this.server, autoRefreshToken: true, new HttpClient(new UnreachableHandler())));
        var session = await client.RetrieveSessionAsync();
        session.Should().NotBeNull("an offline start must not sign the user out");
        await this.persistence.DidNotReceive().DestroySessionAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RetrieveSessionAsync_ShouldDestroyTheSession_GivenTheServerRejectsTheRefreshToken()
    {
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithHeader("Content-Type", "application/json")
                .WithBody(Fixture("token_not_found_error.json")));
        var client = this.Restore(TestClients.Against(this.server, autoRefreshToken: true));
        var session = await client.RetrieveSessionAsync();
        session.Should().BeNull();
        client.CurrentSession.Should().BeNull();
        await this.persistence.Received().DestroySessionAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task LoadSessionAsync_ShouldRestoreAnAsyncOnlyStore_GivenItsSyncMembersThrow()
    {
        var store = SessionPersistenceSubstitute.AsyncOnly();
        await store.SaveSessionAsync(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var client = this.Track(TestClients.Against(this.server));
        client.SetPersistence(store);

        await client.LoadSessionAsync();

        client.CurrentSession!.RefreshToken.Should().Be("a-refresh-token", "the async load restores the persisted session");
        store.DidNotReceive().LoadSession();
        store.DidNotReceive().SaveSession(Arg.Any<Session>());
    }

    [TestMethod]
    public async Task RetrieveSessionAsync_ShouldDestroyAnAsyncOnlyStore_GivenTheServerRejectsTheRefreshToken()
    {
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithHeader("Content-Type", "application/json")
                .WithBody(Fixture("token_not_found_error.json")));
        var store = SessionPersistenceSubstitute.AsyncOnly();
        await store.SaveSessionAsync(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var client = this.Track(TestClients.Against(this.server, autoRefreshToken: true));
        client.SetPersistence(store);
        await client.LoadSessionAsync();

        var session = await client.RetrieveSessionAsync();

        session.Should().BeNull("the server rejected the refresh token, so the session is destroyed");
        (await store.LoadSessionAsync()).Should().BeNull();
        await store.Received().DestroySessionAsync(Arg.Any<CancellationToken>());
        store.DidNotReceive().DestroySession();
    }

    [TestMethod]
    public async Task RetrieveSessionAsync_ShouldKeepTheSession_GivenTheClientIsMarkedOffline()
    {
        var client = this.Restore(TestClients.Against(this.server, autoRefreshToken: true));
        client.Online = false;
        var session = await client.RetrieveSessionAsync();
        session.Should().NotBeNull("an offline client cannot refresh, but must not lose the session");
        await this.persistence.DidNotReceive().DestroySessionAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public void LoadSession_ShouldNotSignOut_GivenAnEmptyStore()
    {
        var client = this.Track(TestClients.Against(this.server));
        var stateChanges = new List<Constants.AuthState>();
        client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        client.SetPersistence(SessionPersistenceSubstitute.Tracking());
        client.LoadSession();
        client.CurrentSession.Should().BeNull();
        stateChanges.Should().NotContain(SignedOut, "a cold start with nothing on either side has no session to sign out of");
    }

    [TestMethod]
    public async Task TokenRefresh_ShouldBackOffBetweenAttempts_GivenARestoredExpiredSession()
    {
        var handler = new UnreachableHandler();
        var client = TestClients.Against(this.server, autoRefreshToken: true, new HttpClient(handler));
        this.persistence.SaveSession(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 60, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        this.Restore(client);
        await handler.Started;
        await Task.Delay(250);
        handler.Attempts.Should().Be(1,
            "the first attempt for an expired session fires immediately, and a failed one must back off a tick before the next");
    }

    [TestMethod]
    public async Task TokenRefresh_ShouldWaitATick_GivenTheRefreshedSessionIsAboutToExpire()
    {
        var handler = new GatedHandler(fixture: "token_success_expiring.json");
        var client = this.Restore(TestClients.Against(this.server, autoRefreshToken: true, new HttpClient(handler)));
        handler.Release();
        await client.RetrieveSessionAsync();
        await Task.Delay(250);
        handler.Attempts.Should().Be(1,
            "the refreshed session expires in a second, so without a floor the timer re-arms at zero and hammers the token endpoint (issue #396)");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldShareOneAttempt_GivenConcurrentCallers()
    {
        var handler = new GatedHandler();
        var client = this.Restore(TestClients.Against(this.server, httpClient: new HttpClient(handler)));
        var first = client.RefreshToken();
        var second = client.RefreshToken();
        handler.Release();
        await Task.WhenAll(first, second);
        handler.Attempts.Should().Be(1, "a refresh token is single-use, so concurrent refreshes must share one attempt");
        await client.RefreshToken();
        handler.Attempts.Should().Be(2, "a completed attempt must not be reused");
        ((Client) client).refreshAttempt.Should().BeNull("a finished attempt must not keep its refresh token in memory");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldDiscardTheResult_GivenTheSessionWasReplacedMidFlight()
    {
        var handler = new GatedHandler();
        var client = this.Restore(TestClients.Against(this.server, httpClient: new HttpClient(handler)));
        var refresh = client.RefreshToken();
        await handler.Started;
        this.persistence.SaveSession(new Session { AccessToken = "another-access-token", RefreshToken = "another-refresh-token", ExpiresIn = 3600 });
        client.LoadSession();
        handler.Release();
        await refresh;
        client.CurrentSession!.RefreshToken.Should().Be("another-refresh-token",
            "the refresh belonged to the session that was replaced, so its result must not overwrite the new one");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldStartItsOwnAttempt_GivenTheSessionWasReplaced()
    {
        var handler = new GatedHandler();
        var client = this.Restore(TestClients.Against(this.server, httpClient: new HttpClient(handler)));
        var stale = client.RefreshToken();
        await handler.Started;
        this.persistence.SaveSession(new Session { AccessToken = "another-access-token", RefreshToken = "another-refresh-token", ExpiresIn = 3600 });
        client.LoadSession();
        var fresh = client.RefreshToken();
        handler.Release();
        await Task.WhenAll(stale, fresh);
        handler.RefreshTokens.Should().Equal(new[] { "a-refresh-token", "another-refresh-token" },
            "the in-flight attempt holds the previous session's refresh token, so the new session must not join it");
    }

    [TestMethod]
    public async Task SignOut_ShouldLeaveNoSessionOrRefreshToken_GivenARefreshInFlight()
    {
        var handler = new GatedHandler();
        var client = this.Restore(TestClients.Against(this.server, httpClient: new HttpClient(handler)));
        var stateChanges = new List<Constants.AuthState>();
        client.AddStateChangedListener((_, state) => stateChanges.Add(state));
        var refresh = client.RefreshToken();
        await handler.Started;
        await client.SignOut();
        ((Client) client).refreshAttempt.Should().BeNull("the refresh token is a secret and must not outlive the sign-out");
        handler.Release();
        await refresh;
        client.CurrentSession.Should().BeNull("the refresh belonged to the session that was signed out");
        stateChanges.Should().NotContain(TokenRefreshed);
    }

    [TestMethod]
    public async Task LoadSession_ShouldKeepTheUserSignedIn_GivenAClientWithoutPersistence()
    {
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(Fixture("token_success.json")));
        var client = this.Track(TestClients.Against(this.server));
        await client.SignIn("test@example.com", "a-password");
        client.LoadSession();
        client.CurrentSession.Should().NotBeNull("a client with no persistence has nothing to load, so LoadSession must leave it alone");
    }

    [TestMethod]
    public async Task LoadSession_ShouldKeepTheSignedInSession_GivenTheSignInCompletedDuringTheRead()
    {
        this.server
            .Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(Fixture("token_success.json")));
        using var readStarted = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        var store = Substitute.For<IGotrueSessionPersistence<Session>>();
        store.LoadSession().Returns(_ =>
        {
            readStarted.Set();
            gate.Wait(HandlerTimeout);
            return new Session { AccessToken = "a-stale-access-token", RefreshToken = "a-stale-token", ExpiresIn = 3600 };
        });
        var client = this.Track(TestClients.Against(this.server));
        client.SetPersistence(store);
        var load = Task.Run(client.LoadSession);
        readStarted.Wait(HandlerTimeout).Should().BeTrue();
        await client.SignIn("test@example.com", "a-password");
        gate.Set();
        await load;
        client.CurrentSession!.RefreshToken.Should().Be("new-refresh-token",
            "the sign-in completed while the store was being read, so the session it produced must not be overwritten by the slower load");
    }

    [TestMethod]
    public async Task RetrieveSessionAsync_ShouldReturnTheReplacement_GivenTheRejectedRefreshWasForAReplacedSession()
    {
        var handler = new GatedHandler(HttpStatusCode.BadRequest, "token_not_found_error.json");
        var client = this.Restore(TestClients.Against(this.server, autoRefreshToken: true, new HttpClient(handler)));
        var retrieve = client.RetrieveSessionAsync();
        await handler.Started;
        this.persistence.SaveSession(new Session { AccessToken = "another-access-token", RefreshToken = "another-refresh-token", ExpiresIn = 3600 });
        client.LoadSession();
        handler.Release();
        var session = await retrieve;
        session!.RefreshToken.Should().Be("another-refresh-token",
            "the rejection was for the replaced session, so the live one must be returned, not null");
        await this.persistence.DidNotReceive().DestroySessionAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RetrieveSessionAsync_ShouldKeepTheReplacementPersisted_GivenTheRejectionLandedAfterASignIn()
    {
        var destroyStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var destroyGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.persistence = HeldDestroyStore(destroyStarted, destroyGate.Task);
        this.persistence.SaveSession(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 });
        var handler = new GatedHandler(HttpStatusCode.BadRequest, "token_not_found_error.json");
        var client = this.Restore(TestClients.Against(this.server, autoRefreshToken: true, new HttpClient(handler)));
        var retrieve = client.RetrieveSessionAsync();
        await handler.Started;
        handler.Release();
        await destroyStarted.Task.WaitAsync(HandlerTimeout);

        // The sign-in installs and persists its session while the rejection's destroy is still in flight.
        this.persistence.SaveSession(new Session { AccessToken = "another-access-token", RefreshToken = "another-refresh-token", ExpiresIn = 3600 });
        client.LoadSession();
        var signIn = client.NotifyAuthStateChangeAsync(SignedIn);
        destroyGate.SetResult(true);
        await signIn;
        await retrieve;

        this.persistence.LoadSession()!.RefreshToken.Should().Be("another-refresh-token",
            "the destroy belonged to the rejected session, so it must not wipe what the sign-in saved (issue #396)");
        client.CurrentSession!.RefreshToken.Should().Be("another-refresh-token");
    }

    [TestMethod]
    public void LoadSession_ShouldClearTheSession_GivenTheStoreWasEmptied()
    {
        var client = this.Restore(TestClients.Against(this.server));
        this.persistence.DestroySession();
        client.LoadSession();
        client.CurrentSession.Should().BeNull("a store emptied behind the client's back must not leave a stale session in memory");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldRetryAfterAFailedAttempt_GivenSequentialCallers()
    {
        var handler = new UnreachableHandler();
        var client = this.Restore(TestClients.Against(this.server, httpClient: new HttpClient(handler)));
        for (var i = 0; i < 2; i++)
        {
            var act = () => client.RefreshToken();
            await act.Should().ThrowAsync<GotrueException>();
        }
        handler.Attempts.Should().Be(2, "a failed attempt must not be cached as the in-flight refresh");
    }

    [TestMethod]
    public void LoadSession_ShouldNotThrow_GivenAStoreThatFailsToLoad()
    {
        var store = Substitute.For<IGotrueSessionPersistence<Session>>();
        store.LoadSession().Returns(_ => throw new IOException("locked"));
        var client = this.Track(TestClients.Against(this.server));
        client.SetPersistence(store);
        var act = () => client.LoadSession();
        act.Should().NotThrow();
        client.CurrentSession.Should().BeNull();
    }

    private IGotrueClient<User, Session> Restore(IGotrueClient<User, Session> client)
    {
        this.Track(client);
        client.SetPersistence(this.persistence);
        client.LoadSession();
        return client;
    }

    private IGotrueClient<User, Session> Track(IGotrueClient<User, Session> client)
    {
        this.clients.Add(client);
        return client;
    }

    /// <summary>
    ///     A tracking store whose async destroy waits on a gate, so a test can hold a sign-out's write in flight.
    /// </summary>
    private static IGotrueSessionPersistence<Session> HeldDestroyStore(TaskCompletionSource<bool> started, Task gate)
    {
        var store = SessionPersistenceSubstitute.Tracking();
        store.DestroySessionAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            started.TrySetResult(true);
            await gate;
            store.DestroySession();
        });
        return store;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TokenRefresh", "Fixtures", name));

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Attempts;

        /// <summary>Completes when the first attempt reaches the handler.</summary>
        public Task Started => this.started.Task.WaitAsync(HandlerTimeout);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.Attempts);
            this.started.TrySetResult(true);
            throw new HttpRequestException("connection refused");
        }
    }

    /// <summary>
    ///     Answers a token refresh, but not before the test opens the gate - so a refresh can be held in
    ///     flight while the test does something to the session behind its back. Every other call, sign-out
    ///     included, is answered straight away.
    /// </summary>
    private sealed class GatedHandler(HttpStatusCode status = HttpStatusCode.OK, string fixture = "token_success.json") : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string?> refreshTokens = new();
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Attempts => this.refreshTokens.Count;

        /// <summary>The refresh token each refresh put on the wire, in the order the requests arrived.</summary>
        public IReadOnlyCollection<string?> RefreshTokens => this.refreshTokens;

        /// <summary>Completes when the first refresh reaches the handler, so a test can act while it is held.</summary>
        public Task Started => this.started.Task.WaitAsync(HandlerTimeout);

        public void Release() => this.gate.TrySetResult(true);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            }
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            this.refreshTokens.Enqueue(body["refresh_token"]?.GetValue<string>());
            this.started.TrySetResult(true);
            await this.gate.Task.WaitAsync(HandlerTimeout, cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(Fixture(fixture), Encoding.UTF8, "application/json"),
            };
        }
    }
}
