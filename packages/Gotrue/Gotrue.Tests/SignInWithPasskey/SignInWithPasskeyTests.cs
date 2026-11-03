#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.SignInWithPasskey;

/// <summary>
///     End-to-end passkey sign-ins against the live stack, with passkeys enabled in <c>supabase/config.toml</c>. Each
///     test signs up its own user, registers a passkey through the auth server's registration ceremony with
///     <see cref="SoftwareAuthenticator" />, signs out, then answers the sign-in challenge with an assertion the
///     software authenticator signs.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SignInWithPasskeyTests : AuthClientFixture
{
    private const string RelyingPartyId = "localhost";

    [TestMethod]
    public async Task StartPasskeySignInAsync_ShouldIssueAChallengeForTheConfiguredRelyingParty()
    {
        var challenge = await this.Client.StartPasskeySignInAsync();
        challenge.Options.GetProperty("rpId").GetString().Should().Be(RelyingPartyId,
            "the request options name the relying party of [auth.webauthn] in supabase/config.toml (supabase/auth getPasskeyWebAuthn)");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldSignInThePasskeysOwner_GivenAssertionFromTheirPasskey()
    {
        var (userId, passkey) = await this.SignUpWithPasskeyThenSignOut();
        var challenge = await this.Client.StartPasskeySignInAsync();
        await this.Client.SignInWithPasskeyAsync(challenge.Id, SoftwareAuthenticator.SignAssertion(passkey, challenge.Options));
        this.Client.CurrentUser!.Id.Should().Be(userId, "a discoverable sign-in resolves the user from the assertion's user handle (supabase/auth PasskeyAuthenticationVerify)");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldRecordThePasskeyAsUsed_GivenAcceptedAssertion()
    {
        var (_, passkey) = await this.SignUpWithPasskeyThenSignOut();
        var challenge = await this.Client.StartPasskeySignInAsync();
        await this.Client.SignInWithPasskeyAsync(challenge.Id, SoftwareAuthenticator.SignAssertion(passkey, challenge.Options));
        (await this.Client.ListPasskeysAsync()).Should().ContainSingle().Which.LastUsedAt.Should().NotBeNull(
            "a sign-in stamps last_used_at on the passkey it used (supabase/auth UpdateLastUsedWithSignCount)");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowVerificationFailed_GivenAssertionFromUnregisteredPasskey()
    {
        var challenge = await this.Client.StartPasskeySignInAsync();
        var signingIn = () => this.Client.SignInWithPasskeyAsync(challenge.Id, SoftwareAuthenticator.SignAssertion(SoftwareAuthenticator.CreateUnregisteredPasskey(), challenge.Options));
        (await signingIn.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("webauthn_verification_failed",
            "the server finds no registered credential for the assertion (supabase/auth PasskeyAuthenticationVerify)");
    }

    [TestMethod]
    public async Task SignInWithPasskeyAsync_ShouldThrowChallengeNotFound_GivenChallengeAlreadyUsed()
    {
        var (_, passkey) = await this.SignUpWithPasskeyThenSignOut();
        var challenge = await this.Client.StartPasskeySignInAsync();
        await this.Client.SignInWithPasskeyAsync(challenge.Id, SoftwareAuthenticator.SignAssertion(passkey, challenge.Options));
        var replaying = () => this.Client.SignInWithPasskeyAsync(challenge.Id, SoftwareAuthenticator.SignAssertion(passkey, challenge.Options));
        (await replaying.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("webauthn_challenge_not_found",
            "a challenge is consumed by its first use, so a replay finds none (supabase/auth ConsumeWebAuthnChallengeByID)");
    }

    private async Task<SignedUpPasskeyOwner> SignUpWithPasskeyThenSignOut()
    {
        var session = await this.SignUpNewUser();
        var passkey = await SoftwareAuthenticator.RegisterSoftwarePasskeyAsync(session.AccessToken!);
        await this.Client.SignOut();
        return new SignedUpPasskeyOwner(session.User!.Id!, passkey);
    }

    private sealed record SignedUpPasskeyOwner(string UserId, SoftwarePasskey Passkey);
}
