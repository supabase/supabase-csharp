#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Mfa;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.AdminDeletePasskey;

/// <summary>
///     End-to-end service-role deletion of a user's passkeys against the live stack. Each test signs up its own users
///     and seeds passkeys through the auth server's registration ceremony with <see cref="SoftwareAuthenticator" />,
///     which needs passkeys enabled in <c>supabase/config.toml</c>; the admin route itself is served either way.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class AdminDeletePasskeyTests : MfaFixture
{
    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldRemoveOnlyThatPasskey_GivenUserOwnsIt()
    {
        var session = await this.SignUpNewUser();
        var kept = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var deleted = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var userId = Guid.Parse(session.User!.Id!);
        await this.Admin.DeletePasskeyAsync(userId: userId, passkeyId: deleted);
        (await this.Admin.ListPasskeysAsync(userId)).Should().ContainSingle().Which.Id.Should().Be(kept,
            "DELETE /admin/users/{user_id}/passkeys/{passkey_id} removes only the passkey it names (supabase/auth AdminPasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowNotFound_GivenUnknownPasskey()
    {
        var session = await this.SignUpNewUser();
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: Guid.Parse(session.User!.Id!), passkeyId: Guid.NewGuid());
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("validation_failed",
            "the auth server answers 404 validation_failed when the user owns no passkey with the id (supabase/auth AdminPasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowNotFound_GivenAnotherUsersPasskey()
    {
        var owner = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(owner.AccessToken!);
        var other = await this.SignUpNewUser();
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: Guid.Parse(other.User!.Id!), passkeyId: passkey);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.StatusCode.Should().Be(404,
            "the auth server looks the passkey up by id and user, so another user's passkey is not found (supabase/auth FindWebAuthnCredentialByIDAndUserID)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldKeepThePasskey_GivenAnotherUsersId()
    {
        var owner = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(owner.AccessToken!);
        var other = await this.SignUpNewUser();
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: Guid.Parse(other.User!.Id!), passkeyId: passkey);
        await deleting.Should().ThrowAsync<GotrueException>();
        (await this.Admin.ListPasskeysAsync(Guid.Parse(owner.User!.Id!))).Should().ContainSingle().Which.Id.Should().Be(passkey,
            "a rejected deletion leaves the owner's passkey in place (supabase/auth AdminPasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowUserNotFound_GivenUnknownUser()
    {
        var deleting = () => this.Admin.DeletePasskeyAsync(userId: Guid.NewGuid(), passkeyId: Guid.NewGuid());
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(FailureHint.Reason.UserNotFound,
            "the auth server answers 404 user_not_found when no user has the id (supabase/auth loadUser)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowAdminTokenRequired_GivenNonAdminKey()
    {
        var session = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var deleting = () => TestClients.AdminAgainstCliStack(TestClients.CliAnonKey).DeletePasskeyAsync(userId: Guid.Parse(session.User!.Id!), passkeyId: passkey);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(FailureHint.Reason.AdminTokenRequired,
            "the auth server answers 403 not_admin to a key without the service_role claim (supabase/auth requireAdmin)");
    }
}
