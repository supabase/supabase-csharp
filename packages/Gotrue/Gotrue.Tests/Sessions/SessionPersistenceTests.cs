#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Sessions;

/// <summary>
///     End-to-end session management against the live stack: restoring a persisted session into a fresh
///     client, and swapping the active session with a caller-supplied access/refresh token pair.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SessionPersistenceTests : AuthClientFixture
{
    [TestMethod]
    public async Task LoadSession_ShouldRestoreSignedInSession_GivenPersistedSession()
    {
        var session = await this.SignUpNewUser();
        this.VerifyGoodSession(session);
        var restoredPersistence = SessionPersistenceSubstitute.Tracking();
        restoredPersistence.SaveSession(session);
        var restoredClient = TestClients.AgainstCliStack();
        restoredClient.SetPersistence(restoredPersistence);
        restoredClient.LoadSession();
        restoredClient.CurrentSession.Should().BeSameAs(restoredPersistence.LoadSession());
        restoredClient.CurrentUser!.Id.Should().Be(session.User!.Id, "loading persistence restores the signed-in user");
        var refreshed = await restoredClient.RetrieveSessionAsync();
        refreshed!.AccessToken.Should().NotBeNull();
        refreshed.RefreshToken.Should().NotBeNull();
        refreshed.User!.Id.Should().Be(session.User.Id);
    }

    [TestMethod]
    public async Task LoadSessionAsync_ShouldRestoreSignedInSession_GivenPersistedSession()
    {
        var session = await this.SignUpNewUser();
        this.VerifyGoodSession(session);
        var restoredPersistence = SessionPersistenceSubstitute.Tracking();
        await restoredPersistence.SaveSessionAsync(session);
        var restoredClient = TestClients.AgainstCliStack();
        restoredClient.SetPersistence(restoredPersistence);
        await restoredClient.LoadSessionAsync();
        restoredClient.CurrentSession.Should().BeSameAs(await restoredPersistence.LoadSessionAsync());
        restoredClient.CurrentUser!.Id.Should().Be(session.User!.Id, "the async load restores the signed-in user");
        var refreshed = await restoredClient.RetrieveSessionAsync();
        refreshed!.AccessToken.Should().NotBeNull();
        refreshed.RefreshToken.Should().NotBeNull();
        refreshed.User!.Id.Should().Be(session.User.Id);
    }

    [TestMethod]
    public async Task SetSession_ShouldRestoreUserFromTokenPairAndForceRefreshWhenRequested()
    {
        await this.SignUpNewUser();
        var originalId = this.Client.CurrentUser!.Id;
        var accessToken = this.Client.CurrentSession!.AccessToken!;
        var refreshToken = this.Client.CurrentSession.RefreshToken!;
        await this.SignUpNewUser();
        this.Client.CurrentSession!.AccessToken.Should().NotBe(accessToken);
        this.StateChanges.Clear();
        await this.Client.SetSession(accessToken, refreshToken);
        this.StateChanges.Should().Contain(SignedIn);
        this.Client.CurrentUser!.Id.Should().Be(originalId);
        this.Client.CurrentSession!.AccessToken.Should().Be(accessToken, "a still-valid session is reused, not regenerated");
        this.Client.CurrentSession.RefreshToken.Should().Be(refreshToken);
        await this.Client.SetSession(accessToken, refreshToken, true);
        this.Client.CurrentUser!.Id.Should().Be(originalId);
        this.Client.CurrentSession!.RefreshToken.Should().NotBe(refreshToken, "forcing a refresh rotates the tokens");
    }
}
