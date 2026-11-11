#region

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Which scopes clear the local session when the server answers a sign-out: local always does, global only once
///     the server ended the sessions or no user is left, and others never does.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignOutLocalSessionTests
{
    private readonly List<AuthState> stateChanges = new();
    private IGotrueClient<User, Session> client = null!;
    private IGotrueSessionPersistence<Session> persistence = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        this.persistence = SessionPersistenceSubstitute.Tracking();
        this.persistence.SaveSession(SignedInSession());
        this.client.SetPersistence(this.persistence);
        this.client.LoadSession();
        this.client.AddStateChangedListener((_, state) => this.stateChanges.Add(state));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    private static IResponseBuilder Rejection(int status, string errorCode) =>
        Response.Create()
            .WithStatusCode(status)
            .WithHeader("Content-Type", "application/json")
            .WithBody($$"""{ "code": {{status}}, "error_code": "{{errorCode}}", "msg": "rejected" }""");

    private void StubLogout(IResponseBuilder response) =>
        this.server.Given(Request.Create().WithPath("/logout").UsingPost()).RespondWith(response);

    private static Session SignedInSession() => new() { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600 };

    private static async Task DestroyLater() => await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

    /// <summary>Answers the logout without yielding, so the SDK continues on the caller's thread and context.</summary>
    private sealed class SynchronousLogoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    [TestMethod]
    public async Task SignOut_ShouldClearTheLocalSession_GivenLocalScopeAndTheServerFails()
    {
        this.StubLogout(Response.Create().WithStatusCode(500));
        var signOut = () => this.client.SignOut(SignOutScope.Local);
        await signOut.Should().ThrowAsync<GotrueException>();
        using (new AssertionScope())
        {
            this.client.CurrentSession.Should().BeNull("a local sign-out signs this device out whatever the server answers");
            this.persistence.LoadSession().Should().BeNull();
        }
    }

    [TestMethod]
    public async Task SignOut_ShouldClearTheLocalSession_GivenLocalScopeAndTheServerAccepts()
    {
        this.StubLogout(Response.Create().WithStatusCode(204));
        await this.client.SignOut(SignOutScope.Local);
        this.client.CurrentSession.Should().BeNull("a local sign-out signs this device out");
    }

    [TestMethod]
    public async Task SignOut_ShouldClearTheLocalSession_GivenGlobalScopeAndTheServerAccepts()
    {
        this.StubLogout(Response.Create().WithStatusCode(204));
        await this.client.SignOut();
        using (new AssertionScope())
        {
            this.client.CurrentSession.Should().BeNull("the server ended every session, this one included");
            this.persistence.LoadSession().Should().BeNull();
            this.stateChanges.Should().Equal(SignedOut);
        }
    }

    [TestMethod]
    [DataRow(500, "unexpected_failure")]
    [DataRow(403, "session_not_found")]
    [DataRow(403, "bad_jwt")]
    [DataRow(401, "no_authorization")]
    public async Task SignOut_ShouldKeepTheLocalSession_GivenGlobalScopeAndTheServerRefuses(int status, string errorCode)
    {
        this.StubLogout(Rejection(status, errorCode));
        var signOut = () => this.client.SignOut();
        await signOut.Should().ThrowAsync<GotrueException>();
        using (new AssertionScope())
        {
            this.client.CurrentSession.Should().NotBeNull(
                "a failed global sign-out keeps the credentials needed to retry it or fall back to local (supabase-js #2786)");
            this.persistence.LoadSession().Should().NotBeNull();
            this.stateChanges.Should().BeEmpty();
        }
    }

    [TestMethod]
    public async Task SignOut_ShouldKeepTheLocalSession_GivenGlobalScopeAndAnUnreachableServer()
    {
        var unreachable = TestClients.Against(this.server, httpClient: new HttpClient(new UnreachableHandler()));
        unreachable.SetPersistence(this.persistence);
        unreachable.LoadSession();
        var signOut = () => unreachable.SignOut();
        await signOut.Should().ThrowAsync<GotrueException>();
        unreachable.CurrentSession.Should().NotBeNull("an offline global sign-out keeps the user signed in so it can be retried (supabase-js #2786)");
    }

    [TestMethod]
    public async Task SignOut_ShouldClearTheLocalSession_GivenGlobalScopeAndTheUserNoLongerExists()
    {
        this.StubLogout(Rejection(403, "user_not_found"));
        await this.client.SignOut();
        using (new AssertionScope())
        {
            this.client.CurrentSession.Should().BeNull("a deleted user has no session left to revoke, so the device is already signed out");
            this.persistence.LoadSession().Should().BeNull();
        }
    }

    [TestMethod]
    public async Task SignOut_ShouldRevokeTheOtherSessions_GivenOthersScope()
    {
        this.StubLogout(Response.Create().WithStatusCode(204));
        await this.client.SignOut(SignOutScope.Others);
        this.server.VerifySingleReceivedRequest().WithPath("/logout").WithQueryParam("scope", "others");
    }

    [TestMethod]
    [DataRow(SignOutScope.Global)]
    [DataRow(SignOutScope.Local)]
    [DataRow(SignOutScope.Others)]
    public void SignOut_ShouldComplete_GivenACallerBlockingOnItsContext(SignOutScope scope)
    {
        this.StubLogout(Response.Create().WithStatusCode(204));
        BlockingCaller.Completes(() => this.client.SignOut(scope)).Should().BeTrue(
            "a library must not resume on the caller's context: a UI thread blocking on the task would deadlock");
    }

    [TestMethod]
    [DataRow(SignOutScope.Global)]
    [DataRow(SignOutScope.Local)]
    public void SignOut_ShouldComplete_GivenABlockingCallerAndAStoreThatDestroysAsynchronously(SignOutScope scope)
    {
        var synchronousClient = TestClients.Against(this.server, httpClient: new HttpClient(new SynchronousLogoutHandler()));
        var store = SessionPersistenceSubstitute.Tracking();
        store.SaveSession(SignedInSession());
        store.DestroySessionAsync(Arg.Any<CancellationToken>()).Returns(_ => DestroyLater());
        synchronousClient.SetPersistence(store);
        synchronousClient.LoadSession();
        BlockingCaller.Completes(() => synchronousClient.SignOut(scope)).Should().BeTrue(
            "clearing the session must not resume on the caller's context, even when the server answered without leaving it");
    }

    [TestMethod]
    public async Task SignOut_ShouldKeepTheLocalSession_GivenOthersScope()
    {
        this.StubLogout(Response.Create().WithStatusCode(204));
        await this.client.SignOut(SignOutScope.Others);
        using (new AssertionScope())
        {
            this.client.CurrentSession.Should().NotBeNull("the others scope signs out every session except this one");
            this.persistence.LoadSession().Should().NotBeNull();
        }
    }
}
