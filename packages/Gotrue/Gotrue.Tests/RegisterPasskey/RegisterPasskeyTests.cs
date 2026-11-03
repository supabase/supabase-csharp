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

namespace Gotrue.Tests.RegisterPasskey;

/// <summary>
///     End-to-end passkey registrations against the live stack, with passkeys enabled in <c>supabase/config.toml</c>.
///     Each test signs up its own user; <see cref="SoftwareAuthenticator" /> answers the creation options the SDK
///     fetched, as a platform authenticator would. The MFA scenario enrolls a TOTP factor to reach the
///     assurance-level rule.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RegisterPasskeyTests : MfaFixture
{
    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldAddThePasskeyToTheUsersPasskeys_GivenCredentialAnsweringTheChallenge()
    {
        await this.SignUpNewUser();
        var challenge = await this.Client.StartPasskeyRegistrationAsync();
        var passkey = await this.Client.RegisterPasskeyAsync(challenge.Id, SoftwareAuthenticator.CreateCredential(challenge.Options));
        (await this.Client.ListPasskeysAsync()).Should().ContainSingle().Which.Id.Should().Be(passkey.Id,
            "a verified registration stores the credential as one of the user's passkeys (supabase/auth PasskeyRegistrationVerify)");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldReturnAPasskeyNeverUsedToSignIn_GivenCredentialAnsweringTheChallenge()
    {
        await this.SignUpNewUser();
        var challenge = await this.Client.StartPasskeyRegistrationAsync();
        var passkey = await this.Client.RegisterPasskeyAsync(challenge.Id, SoftwareAuthenticator.CreateCredential(challenge.Options));
        passkey.Should().BeEquivalentTo(new { FriendlyName = "Passkey", LastUsedAt = (DateTimeOffset?) null },
            "the server names a passkey from an unknown authenticator model \"Passkey\" and answers without last_used_at (supabase/auth PasskeyMetadataResponse)");
    }

    [TestMethod]
    public async Task RegisterPasskeyAsync_ShouldThrowChallengeNotFound_GivenChallengeAlreadyUsed()
    {
        await this.SignUpNewUser();
        var challenge = await this.Client.StartPasskeyRegistrationAsync();
        await this.Client.RegisterPasskeyAsync(challenge.Id, SoftwareAuthenticator.CreateCredential(challenge.Options));
        var replaying = () => this.Client.RegisterPasskeyAsync(challenge.Id, SoftwareAuthenticator.CreateCredential(challenge.Options));
        (await replaying.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("webauthn_challenge_not_found",
            "a challenge is consumed by its first use, so a replay finds none (supabase/auth ConsumeWebAuthnChallengeByID)");
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldThrowInsufficientAal_GivenMfaUserWithAal1Session()
    {
        await this.SignUpNewUser();
        var enrollment = await this.EnrollTotp();
        await this.Client.ChallengeAndVerify(new MfaChallengeAndVerifyParams { FactorId = enrollment.Id, Code = TotpCode(enrollment) });
        await this.Client.SignIn(this.Client.CurrentUser!.Email!, Password);
        var starting = () => this.Client.StartPasskeyRegistrationAsync();
        (await starting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "a password sign-in is aal1, and a user with MFA enabled needs aal2 to register a passkey (supabase/auth requirePasskeyManagementAAL)");
    }

    [TestMethod]
    public async Task StartPasskeyRegistrationAsync_ShouldThrowNoAuthorization_GivenAnonymousUser()
    {
        await this.Client.SignInAnonymously();
        var starting = () => this.Client.StartPasskeyRegistrationAsync();
        (await starting.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("no_authorization",
            "the registration routes reject anonymous users before the handler runs (supabase/auth requireNotAnonymous)");
    }
}
