#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Mfa;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Mfa;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.DeletePasskey;

/// <summary>
///     End-to-end deletion of the signed-in user's passkeys against the live stack, with passkeys enabled in
///     <c>supabase/config.toml</c>. Passkeys are seeded through the auth server's registration ceremony by
///     <see cref="SoftwareAuthenticator" />, each test on its own freshly signed-up user; the MFA scenarios enroll
///     a TOTP factor to reach the assurance-level rule.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class DeletePasskeyTests : MfaFixture
{
    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldRemoveThePasskeyFromTheUsersPasskeys_GivenUserOwnsIt()
    {
        var session = await this.SignUpNewUser();
        var kept = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var deleted = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        await this.Client.DeletePasskeyAsync(deleted);
        (await this.Client.ListPasskeysAsync()).Should().ContainSingle().Which.Id.Should().Be(kept,
            "DELETE /passkeys/{passkey_id} revokes only the passkey it names (supabase/auth PasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowNotFound_GivenUnknownPasskey()
    {
        await this.SignUpNewUser();
        var deleting = () => this.Client.DeletePasskeyAsync(Guid.NewGuid());
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.StatusCode.Should().Be(404,
            "a passkey the user does not own is reported as not found (supabase/auth PasskeyDelete)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldThrowInsufficientAal_GivenMfaUserWithAal1Session()
    {
        var passkey = await this.SignUpWithPasskeyAndVerifiedTotp();
        await this.Client.SignIn(this.Client.CurrentUser!.Email!, Password);
        var deleting = () => this.Client.DeletePasskeyAsync(passkey);
        (await deleting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "a password sign-in is aal1, and a user with MFA enabled needs aal2 to delete a passkey (supabase/auth requirePasskeyManagementAAL)");
    }

    [TestMethod]
    public async Task DeletePasskeyAsync_ShouldRemoveThePasskey_GivenMfaUserWithAal2Session()
    {
        var passkey = await this.SignUpWithPasskeyAndVerifiedTotp();
        await this.Client.DeletePasskeyAsync(passkey);
        (await this.Client.ListPasskeysAsync()).Should().BeEmpty(
            "verifying the TOTP factor raised the session to aal2, which may delete passkeys (supabase/auth requirePasskeyManagementAAL)");
    }

    private async Task<Guid> SignUpWithPasskeyAndVerifiedTotp()
    {
        var session = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var enrollment = await this.EnrollTotp();
        await this.Client.ChallengeAndVerify(new MfaChallengeAndVerifyParams { FactorId = enrollment.Id, Code = TotpCode(enrollment) });
        return passkey;
    }
}
