#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Supabase.Gotrue;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.SignOut;

/// <summary>
///     Pins the reason a listener reads from the client on each sign-out: an explicit sign-out, a refresh token
///     the server rejected, and a stored session that disappeared, plus the reason staying absent otherwise.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignOutReasonContractTests
{
    private const string Spec = "capability auth.session.sign_out_reason";
    private static readonly TimeSpan SlowDestroy = TimeSpan.FromMilliseconds(50);
    private readonly List<SignOutReason?> reasons = new();
    private IGotrueClient<User, Session> client = null!;
    private IGotrueSessionPersistence<Session> persistence = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.persistence = SessionPersistenceSubstitute.Tracking();
        this.persistence.SaveSession(StoredSession());
        this.client = TestClients.Against(this.server);
        this.client.SetPersistence(this.persistence);
        this.client.AddStateChangedListener((sender, state) =>
        {
            if (state == SignedOut)
            {
                this.reasons.Add(sender.SignOutReason);
            }
        });
        this.client.LoadSession();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.client.Shutdown();
        this.server.Dispose();
    }

    [TestMethod]
    public void SignOutReason_ShouldBeAbsent_GivenASignedInUser() =>
        this.client.SignOutReason.Should().BeNull("the reason is only meaningful once the user is signed out (" + Spec + ")");

    [TestMethod]
    [DataRow(SignOutScope.Global)]
    [DataRow(SignOutScope.Local)]
    public async Task SignOut_ShouldReportUserInitiated(SignOutScope scope)
    {
        this.StubSignOut();
        await this.client.SignOut(scope);
        this.reasons.Should().Equal(new SignOutReason?[] { SignOutReason.UserInitiated },
            "the application asked to end the current session (" + Spec + ")");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldReportSessionExpired_GivenTheServerRejectsTheRefreshToken()
    {
        this.StubRejectedRefresh();
        var refresh = () => this.client.RefreshToken();
        await refresh.Should().ThrowAsync<GotrueException>();
        this.reasons.Should().Equal(new SignOutReason?[] { SignOutReason.SessionExpired },
            "a rejected refresh token removes the session without the user asking (" + Spec + ")");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldReportSessionExpired_GivenTheServerRejectsTheSuppliedRefreshToken()
    {
        this.StubRejectedRefresh();
        var refresh = () => this.client.RefreshToken(StoredSession().AccessToken!, StoredSession().RefreshToken!);
        await refresh.Should().ThrowAsync<GotrueException>();
        this.reasons.Should().Equal(new SignOutReason?[] { SignOutReason.SessionExpired },
            "a rejected refresh token removes the session without the user asking (" + Spec + ")");
    }

    [TestMethod]
    public void LoadSession_ShouldReportSessionMissing_GivenTheStoreWasEmptied()
    {
        this.persistence.DestroySession();
        this.client.LoadSession();
        this.reasons.Should().Equal(new SignOutReason?[] { SignOutReason.SessionMissing },
            "the stored session could not be recovered because it was absent (" + Spec + ")");
    }

    [TestMethod]
    public async Task SignOutReason_ShouldBeAbsent_GivenTheUserSignedInAgain()
    {
        this.StubSignOut();
        await this.client.SignOut();
        this.persistence.SaveSession(StoredSession());
        this.client.LoadSession();
        this.client.SignOutReason.Should().BeNull("a signed-in user has no sign-out reason (" + Spec + ")");
    }

    [TestMethod]
    public void SignOut_ShouldComplete_GivenACallerBlockingOnItsContext()
    {
        var slowStore = SessionPersistenceSubstitute.Tracking();
        slowStore.DestroySessionAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.Delay(SlowDestroy));
        var signedOutClient = TestClients.Against(this.server);
        signedOutClient.SetPersistence(slowStore);
        BlockingCaller.Completes(() => signedOutClient.SignOut()).Should().BeTrue(
            "a library must not resume on the caller's synchronization context (CONVENTIONS §4)");
    }

    private static Session StoredSession() =>
        new() { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600, CreatedAt = DateTime.UtcNow };

    private void StubSignOut() =>
        this.server.Given(Request.Create().WithPath("/logout").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(204));

    private void StubRejectedRefresh() =>
        this.server.Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithHeader("Content-Type", "application/json")
                .WithBody(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TokenRefresh", "Fixtures", "token_not_found_error.json"))));
}
