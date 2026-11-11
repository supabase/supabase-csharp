#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.ConcurrentPkceFlows;

/// <summary>
///     End-to-end PKCE flows in flight at the same time on one client, against the live stack: a magic link and a
///     password reset, each started with the PKCE flow before either completes. The client stores no verifier, so each
///     flow is completed with the verifier its own start returned, in any order. The links are read from the stack's
///     local mail server.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ConcurrentPkceFlowsTests : AuthClientFixture
{
    [TestMethod]
    public async Task ExchangeCodeForSession_ShouldCompleteEachFlowWithItsOwnVerifier_GivenTwoPkceFlowsInFlight()
    {
        var flows = await this.StartMagicLinkThenPasswordResetFlows();
        var resetSession = await this.Client.ExchangeCodeForSession(flows.PasswordReset.Verifier, await Mailbox.ClickForAuthCodeAsync(flows.PasswordReset.Email));
        var magicLinkSession = await this.Client.ExchangeCodeForSession(flows.MagicLink.Verifier, await Mailbox.ClickForAuthCodeAsync(flows.MagicLink.Email));
        new[] { magicLinkSession!.User!.Email, resetSession!.User!.Email }.Should().Equal(new[] { flows.MagicLink.Email, flows.PasswordReset.Email },
            "starting the second flow did not replace the first flow's verifier, so each code exchanges for its own user (auth.sign_in.concurrent_pkce_flows)");
    }

    [TestMethod]
    public async Task ExchangeCodeForSession_ShouldThrow_GivenTheVerifierOfAnotherFlowInFlight()
    {
        var flows = await this.StartMagicLinkThenPasswordResetFlows();
        var exchanging = async () => await this.Client.ExchangeCodeForSession(flows.PasswordReset.Verifier, await Mailbox.ClickForAuthCodeAsync(flows.MagicLink.Email));
        await exchanging.Should().ThrowAsync<GotrueException>(
            "the server binds each code to its own flow's challenge, so another flow's verifier never completes it (RFC 7636 §4.6)");
    }

    private async Task<InFlightFlows> StartMagicLinkThenPasswordResetFlows()
    {
        var magicLinkEmail = await this.SignUpThenSignOut();
        var passwordResetEmail = await this.SignUpThenSignOut();
        var magicLink = await this.Client.SignInWithOtp(new SignInWithPasswordlessEmailOptions(magicLinkEmail) { FlowType = OAuthFlowType.PKCE });
        var passwordReset = await this.Client.ResetPasswordForEmail(new ResetPasswordForEmailOptions(passwordResetEmail) { FlowType = OAuthFlowType.PKCE });
        return new InFlightFlows(new InFlightFlow(magicLinkEmail, magicLink.PKCEVerifier!), new InFlightFlow(passwordResetEmail, passwordReset.PKCEVerifier!));
    }

    private async Task<string> SignUpThenSignOut()
    {
        var email = RandomEmail();
        await this.Client.SignUpAsync(SignUpType.Email, email, Password);
        await this.Client.SignOut();
        return email;
    }

    private sealed record InFlightFlow(string Email, string Verifier);

    private sealed record InFlightFlows(InFlightFlow MagicLink, InFlightFlow PasswordReset);
}
