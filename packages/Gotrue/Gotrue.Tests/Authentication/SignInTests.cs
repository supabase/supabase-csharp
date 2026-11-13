#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     End-to-end password and refresh-token sign-in against the live stack: signing back in with an email or
///     phone credential, and exchanging a refresh token for a rotated session without a fresh sign-in.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SignInTests : AuthClientFixture
{
    [TestMethod]
    public async Task SignIn_ShouldReturnSignedInSession_GivenEmailAndPassword()
    {
        var email = RandomEmail();
        await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password);
        await this.Client.SignOut();
        this.StateChanges.Clear();
        var session = await this.Client.SignIn(email, Password);
        this.VerifyGoodSession(session);
    }

    [TestMethod]
    public async Task SignIn_ShouldReturnSignedInSession_GivenPhoneAndPassword()
    {
        var phone = GetRandomPhoneNumber();
        await this.Client.SignUpAsync(Constants.SignUpType.Phone, phone, Password);
        await this.Client.SignOut();
        this.StateChanges.Clear();
        var session = await this.Client.SignIn(Constants.SignInType.Phone, phone, Password);
        this.VerifyGoodSession(session);
    }

    [TestMethod]
    public async Task SignIn_ShouldRotateSessionAsTokenRefresh_GivenRefreshToken()
    {
        var signedUp = (await this.Client.SignUpAsync(Constants.SignUpType.Email, RandomEmail(), Password)).Session;
        var refreshToken = signedUp!.RefreshToken ?? throw new InvalidOperationException();
        this.StateChanges.Clear();
        var refreshed = (await this.Client.SignIn(Constants.SignInType.RefreshToken, refreshToken))!;
        this.Persistence.LoadSession().Should().BeSameAs(this.Client.CurrentSession);
        this.StateChanges.Should().Contain(TokenRefreshed).And.NotContain(SignedIn,
            "exchanging a refresh token rotates the existing session rather than starting a new sign-in");
        refreshed.AccessToken.Should().NotBeNull();
        refreshed.RefreshToken.Should().NotBeNull();
        refreshed.User.Should().NotBeNull();
    }
}
