#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Users;

/// <summary>
///     End-to-end password lifecycle against the live stack: changing a password and signing back in with it,
///     and requesting a reset email (standard and PKCE) which returns a verifier without changing state.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class PasswordManagementTests : AuthClientFixture
{
    [TestMethod]
    public async Task Update_ShouldChangePasswordAndAllowSignInWithTheNewPassword()
    {
        var email = RandomEmail();
        const string newPassword = "IAmANewSecretPassword";
        await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password);
        await this.Client.Update(new UserAttributes { Password = newPassword });
        await this.Client.SignOut();
        var session = await this.Client.SignIn(email, newPassword);
        session.Should().NotBeNull();
    }

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldSucceed_GivenRegisteredEmail()
    {
        var email = RandomEmail();
        await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password);
        (await this.Client.ResetPasswordForEmail(email)).Should().BeTrue();
    }

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldReturnPkceVerifier_GivenPkceFlow()
    {
        var email = RandomEmail();
        await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password);
        var state = await this.Client.ResetPasswordForEmail(new ResetPasswordForEmailOptions(email)
        {
            RedirectTo = "http://localhost:3000",
            FlowType = Constants.OAuthFlowType.PKCE,
        });
        state.PKCEVerifier.Should().NotBeNullOrEmpty();
    }
}
