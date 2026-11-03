#region

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Mfa;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.AdminListPasskeys;

/// <summary>
///     End-to-end service-role listing of a user's passkeys against the live stack. Each test signs up its own user
///     and seeds passkeys through the auth server's registration ceremony with <see cref="SoftwareAuthenticator" />,
///     which needs passkeys enabled in <c>supabase/config.toml</c>; the admin route itself is served either way.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class AdminListPasskeysTests : MfaFixture
{
    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnTheUsersPasskeysOldestFirst_GivenUserWithPasskeys()
    {
        var session = await this.SignUpNewUser();
        var first = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var second = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        (await this.Admin.ListPasskeysAsync(Guid.Parse(session.User!.Id!))).Select(passkey => passkey.Id).Should().Equal(new[] { first, second },
            "GET /admin/users/{user_id}/passkeys lists every passkey of the user ordered by created_at (supabase/auth AdminPasskeyList)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldReturnEmptyList_GivenUserWithoutPasskeys()
    {
        var session = await this.SignUpNewUser();
        (await this.Admin.ListPasskeysAsync(Guid.Parse(session.User!.Id!))).Should().BeEmpty(
            "the auth server answers an empty array for a user who registered no passkey (supabase/auth AdminPasskeyList)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldThrowUserNotFound_GivenUnknownUser()
    {
        var listing = () => this.Admin.ListPasskeysAsync(Guid.NewGuid());
        (await listing.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(FailureHint.Reason.UserNotFound,
            "the auth server answers 404 user_not_found when no user has the id (supabase/auth loadUser)");
    }

    [TestMethod]
    public async Task ListPasskeysAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var session = await this.SignUpNewUser();
        var listing = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).ListPasskeysAsync(Guid.Parse(session.User!.Id!));
        (await listing.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim (supabase/auth requireAdmin)");
    }
}
