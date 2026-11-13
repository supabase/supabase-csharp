#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     End-to-end passwordless (magic link) sign-in against the live stack: requesting a login email neither
///     signs the user in nor changes auth state, it only dispatches the link.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class PasswordlessSignInTests : AuthClientFixture
{
    [TestMethod]
    public async Task SignIn_ShouldSendMagicLinkWithoutChangingState_GivenEmailOnly()
    {
        var email = RandomEmail();
        await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password);
        await this.Client.SignOut();
        this.StateChanges.Clear();
        var sent = await this.Client.SignIn(email);
        sent.Should().BeTrue();
        this.StateChanges.Should().BeEmpty("sending a magic link does not sign the user in");
        this.Persistence.LoadSession().Should().BeSameAs(this.Client.CurrentSession);
    }

    [TestMethod]
    public async Task SendMagicLink_ShouldSucceedWithAndWithoutRedirect()
    {
        var known = RandomEmail();
        await this.Client.SignUpAsync(Constants.SignUpType.Email, known, Password);
        await this.Client.SignOut();
        (await this.Client.SendMagicLink(known)).Should().BeTrue();
        (await this.Client.SendMagicLink(RandomEmail(), new SignInOptions { RedirectTo = $"com.{RandomString(12)}.deeplink://login" }))
            .Should().BeTrue();
    }
}
