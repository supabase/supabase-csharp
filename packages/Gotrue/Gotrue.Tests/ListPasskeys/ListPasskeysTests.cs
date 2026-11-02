#region

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Gotrue.Tests.ListPasskeys;

/// <summary>
///     End-to-end listing of the signed-in user's passkeys against the live stack, with passkeys enabled in
///     <c>supabase/config.toml</c>. Passkeys are seeded through the auth server's registration ceremony by
///     <see cref="SoftwareAuthenticator" />, each test on its own freshly signed-up user.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ListPasskeysTests : AuthClientFixture
{
    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEveryRegisteredPasskeyInRegistrationOrder_GivenUserWithPasskeys()
    {
        var session = await this.SignUpNewUser();
        var first = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var second = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var passkeys = await this.Client.ListPasskeysAsync();
        passkeys.Select(passkey => passkey.Id).Should().Equal(new[] { first, second },
            "GET /passkeys lists the user's passkeys ordered by created_at ascending (supabase/auth PasskeyList)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldDescribeAPasskeyNeverUsedToSignIn_GivenFreshlyRegisteredPasskey()
    {
        var session = await this.SignUpNewUser();
        var registered = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var passkeys = await this.Client.ListPasskeysAsync();
        passkeys.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = registered, FriendlyName = "Passkey", LastUsedAt = (DateTime?) null },
            "the server names a passkey from an unknown authenticator model \"Passkey\" and sets last_used_at only on sign-in (supabase/auth PasskeyFriendlyName)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEmptyList_GivenUserWithoutPasskeys()
    {
        await this.SignUpNewUser();
        var passkeys = await this.Client.ListPasskeysAsync();
        passkeys.Should().BeEmpty("a freshly signed-up user has registered no passkey");
    }
}
