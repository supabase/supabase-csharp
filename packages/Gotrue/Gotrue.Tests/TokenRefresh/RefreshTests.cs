#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.TokenRefresh;

/// <summary>
///     End-to-end session refresh against the live stack: refreshing rotates the refresh token and yields an
///     access token the server accepts (including for an already-expired session), while a rejected refresh
///     token throws a <see cref="GotrueException" /> and destroys the session.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RefreshTests : AuthClientFixture
{
    [TestMethod]
    public async Task RefreshSession_ShouldRotateRefreshTokenAndKeepTheUser()
    {
        var signedUp = await this.SignUpNewUser();
        var refreshed = await this.Client.RefreshSession();
        await VerifyRotatedSession(signedUp, refreshed);
    }

    [TestMethod]
    public async Task RefreshSession_ShouldSucceed_GivenExpiredSession()
    {
        var signedUp = await this.SignUpNewUser();
        this.Client.CurrentSession!.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var refreshed = await this.Client.RefreshSession();
        await VerifyRotatedSession(signedUp, refreshed);
    }

    [TestMethod]
    [DataRow("bogus-token", DisplayName = "malformed token")]
    [DataRow("abcdef012345", DisplayName = "well-formed unknown token")]
    public async Task RefreshSession_ShouldThrowAndDestroySession_GivenRejectedToken(string rejectedToken)
    {
        await this.SignUpNewUser();
        this.Client.CurrentSession!.RefreshToken = rejectedToken;
        var refresh = () => this.Client.RefreshSession();
        await refresh.Should().ThrowAsync<GotrueException>();
        this.Client.CurrentSession.Should().BeNull(
            "a definitive (4xx) refresh rejection destroys the session — a malformed token comes back as the generic validation_failed, so this cannot depend on the specific reason");
    }

    private async Task VerifyRotatedSession(Session original, Session? refreshed)
    {
        refreshed.Should().NotBeNull();
        refreshed!.RefreshToken.Should().NotBe(original.RefreshToken);
        this.StateChanges.Should().Contain(TokenRefreshed);
        this.Persistence.LoadSession().Should().BeSameAs(this.Client.CurrentSession);
        var user = await this.Client.GetUser(refreshed.AccessToken!);
        user!.Id.Should().Be(original.User!.Id);
    }
}
