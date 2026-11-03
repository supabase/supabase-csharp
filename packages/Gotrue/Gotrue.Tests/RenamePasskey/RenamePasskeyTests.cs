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

namespace Gotrue.Tests.RenamePasskey;

/// <summary>
///     End-to-end renaming of the signed-in user's passkeys against the live stack, with passkeys enabled in
///     <c>supabase/config.toml</c>. Passkeys are seeded through the auth server's registration ceremony by
///     <see cref="SoftwareAuthenticator" />, each test on its own freshly signed-up user; the MFA scenario enrolls a
///     TOTP factor to show renaming has no assurance-level rule.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RenamePasskeyTests : MfaFixture
{
    private const string NewName = "Work laptop";
    private const int ServerNameLimit = 120;

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldReturnThePasskeyWithItsNewName_GivenUserOwnsIt()
    {
        var session = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var renamed = await this.Client.RenamePasskeyAsync(passkey, NewName);
        renamed.Should().BeEquivalentTo(new { Id = passkey, FriendlyName = NewName },
            "PATCH /passkeys/{passkey_id} answers with the updated passkey (supabase/auth PasskeyUpdate)");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldStoreTheNewName_GivenUserOwnsThePasskey()
    {
        var session = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        await this.Client.RenamePasskeyAsync(passkey, NewName);
        (await this.Client.ListPasskeysAsync()).Should().ContainSingle().Which.FriendlyName.Should().Be(NewName,
            "the new friendly_name is persisted on the passkey (supabase/auth PasskeyUpdate)");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrowNotFound_GivenUnknownPasskey()
    {
        await this.SignUpNewUser();
        var renaming = () => this.Client.RenamePasskeyAsync(Guid.NewGuid(), NewName);
        (await renaming.Should().ThrowAsync<GotrueException>()).Which.StatusCode.Should().Be(404,
            "a passkey the user does not own is reported as not found (supabase/auth PasskeyUpdate)");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldThrowValidationFailed_GivenNameLongerThanTheServerLimit()
    {
        var session = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var renaming = () => this.Client.RenamePasskeyAsync(passkey, new string('n', ServerNameLimit + 1));
        (await renaming.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("validation_failed",
            "the server rejects a friendly_name longer than 120 characters (supabase/auth PasskeyUpdate)");
    }

    [TestMethod]
    public async Task RenamePasskeyAsync_ShouldRenameThePasskey_GivenMfaUserWithAal1Session()
    {
        var session = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterPasskeyAsync(session.AccessToken!);
        var enrollment = await this.EnrollTotp();
        await this.Client.ChallengeAndVerify(new MfaChallengeAndVerifyParams { FactorId = enrollment.Id, Code = TotpCode(enrollment) });
        await this.Client.SignIn(this.Client.CurrentUser!.Email!, Password);
        (await this.Client.RenamePasskeyAsync(passkey, NewName)).FriendlyName.Should().Be(NewName,
            "unlike deletion, renaming a passkey has no aal2 rule for users with MFA enabled (supabase/auth PasskeyUpdate)");
    }
}
