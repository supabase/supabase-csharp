#region

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.Stateless;

/// <summary>
///     End-to-end authentication through the stateless client against the live stack: reading settings, and
///     signing up / in / out (password, phone, refresh token, magic link, provider) where every call carries
///     its own options rather than a stored session.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class StatelessAuthenticationTests : StatelessFixture
{
    [TestMethod]
    public async Task Settings_ShouldReturnServerConfiguration()
    {
        var settings = await this.Client.Settings(Options);
        settings.Should().NotBeNull();
        using (new AssertionScope())
        {
            settings!.ExternalProviders!["email"].Should().BeTrue();
            settings.ExternalProviders["zoom"].Should().BeFalse();
            settings.DisableSignup.Should().BeFalse();
            settings.MailerAutoConfirm.Should().BeTrue();
            settings.PhoneAutoConfirm.Should().BeTrue();
            settings.SmsProvider.Should().NotBeNull();
        }
    }

    [TestMethod]
    public async Task SignUp_ShouldReturnSession_GivenEmailThenPhoneWithMetadata()
    {
        var emailSession = (await this.Client.SignUp(RandomEmail(), Password, Options))!;
        emailSession.AccessToken.Should().NotBeNull();
        emailSession.RefreshToken.Should().NotBeNull();
        emailSession.User.Should().NotBeNull();
        var phoneSession = (await this.Client.SignUp(SignUpType.Phone, GetRandomPhoneNumber(), Password, Options,
            new SignUpOptions { Data = new Dictionary<string, object> { { "firstName", "Testing" } } }))!;
        phoneSession.AccessToken.Should().NotBeNull();
        phoneSession.User!.UserMetadata["firstName"].Should().Be("Testing");
    }

    [TestMethod]
    public async Task SignUp_ShouldThrow_GivenDuplicateEmail()
    {
        var email = RandomEmail();
        (await this.Client.SignUp(email, Password, Options)).Should().NotBeNull();
        var duplicate = () => this.Client.SignUp(email, Password, Options);
        await duplicate.Should().ThrowAsync<GotrueException>();
    }

    [TestMethod]
    public async Task SignIn_ShouldReturnSession_GivenEmailPhoneAndRefreshToken()
    {
        var email = RandomEmail();
        await this.Client.SignUp(email, Password, Options);
        var emailSession = (await this.Client.SignIn(email, Password, Options))!;
        emailSession.AccessToken.Should().NotBeNull();
        emailSession.User.Should().NotBeNull();
        var phone = GetRandomPhoneNumber();
        await this.Client.SignUp(SignUpType.Phone, phone, Password, Options);
        var phoneSession = (await this.Client.SignIn(SignInType.Phone, phone, Password, Options))!;
        phoneSession.AccessToken.Should().NotBeNull();
        var refreshed = (await this.Client.RefreshToken(phoneSession.AccessToken!, phoneSession.RefreshToken!, Options))!;
        refreshed.AccessToken.Should().NotBeNull();
        refreshed.RefreshToken.Should().NotBeNull();
        refreshed.User.Should().NotBeNull();
    }

    [TestMethod]
    public async Task SignOut_ShouldSucceed_GivenAccessToken()
    {
        var email = RandomEmail();
        await this.Client.SignUp(email, Password, Options);
        var session = (await this.Client.SignIn(email, Password, Options))!;
        (await this.Client.SignOut(session.AccessToken!, Options)).Should().BeTrue();
    }

    [TestMethod]
    public async Task SignIn_ShouldSendMagicLink_GivenEmailOnly()
    {
        var known = RandomEmail();
        await this.Client.SignUp(known, Password, Options);
        (await this.Client.SignIn(known, Options)).Should().BeTrue();
        (await this.Client.SignIn(RandomEmail(), Options, new SignInOptions { RedirectTo = $"com.{RandomString(12)}.deeplink://login" }))
            .Should().BeTrue();
    }

    [TestMethod]
    public void SignIn_ShouldBuildProviderAuthorizeUrl_GivenProvider()
    {
        // Provider-side state is owned by the GoTrue server; the SDK must not inject its own (issue #377).
        var result = this.Client.SignIn(Provider.Google, Options);
        result.Uri.ToString().Should().StartWith($"{TestClients.CliAuthUrl}/authorize");
        result.Uri.Query.Should().Contain("provider=google").And.NotContain("state=");
        var scoped = this.Client.SignIn(Provider.Google, Options, new SignInOptions { Scopes = "special scopes please" });
        scoped.Uri.Query.Should().Contain("scopes=special+scopes+please").And.NotContain("state=");
    }

    [TestMethod]
    public async Task Update_ShouldPersistUserMetadata()
    {
        var email = RandomEmail();
        var session = (await this.Client.SignUp(email, Password, Options))!;
        var updated = (await this.Client.Update(session.AccessToken!, new UserAttributes
        {
            Data = new Dictionary<string, object> { { "hello", "world" } },
        }, Options))!;
        updated.Email.Should().Be(email);
        updated.UserMetadata.Should().ContainKey("hello");
    }

    [TestMethod]
    public async Task SignIn_ShouldThrow_GivenWrongPassword()
    {
        var email = RandomEmail();
        await this.Client.SignUp(email, Password, Options);
        var signIn = () => this.Client.SignIn(email, Password + "$", Options);
        await signIn.Should().ThrowAsync<GotrueException>();
    }
}
