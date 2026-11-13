#region

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Mfa;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Mfa;

/// <summary>
///     End-to-end TOTP multi-factor flow against the live stack: enrolling a factor, verifying it to raise the
///     session to AAL2, then challenge-and-verify on a fresh sign-in, and finally unenrolling back to AAL1.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class MfaEnrollmentTests : MfaFixture
{
    [TestMethod]
    public async Task Totp_ShouldRaiseAssuranceToAal2AndReturnToAal1OnUnenroll()
    {
        var email = RandomEmail();
        var session = (await this.Client.SignUpAsync(Constants.SignUpType.Email, email, Password)).Session;
        this.VerifyGoodSession(session);
        var enrollment = await this.EnrollTotp();
        enrollment.Id.Should().NotBeNull();
        enrollment.FriendlyName.Should().Be("Enroll test");
        enrollment.Type.Should().Be("totp");
        var challenge = (await this.Client.Challenge(new MfaChallengeParams { FactorId = enrollment.Id }))!;
        challenge.Id.Should().NotBeNull();
        var verified = await this.Client.Verify(new MfaVerifyParams
        {
            FactorId = enrollment.Id,
            ChallengeId = challenge.Id,
            Code = TotpCode(enrollment),
        });
        this.VerifyGoodSession(verified);
        await this.Client.SignOut();
        await this.Client.SignIn(email, Password);
        var afterSignIn = (await this.Client.GetAuthenticatorAssuranceLevel())!;
        afterSignIn.CurrentLevel.Should().Be(AuthenticatorAssuranceLevel.aal1);
        afterSignIn.NextLevel.Should().Be(AuthenticatorAssuranceLevel.aal2);
        await this.Client.ChallengeAndVerify(new MfaChallengeAndVerifyParams { FactorId = enrollment.Id, Code = TotpCode(enrollment) });
        var elevated = (await this.Client.GetAuthenticatorAssuranceLevel())!;
        elevated.CurrentLevel.Should().Be(AuthenticatorAssuranceLevel.aal2);
        elevated.NextLevel.Should().Be(AuthenticatorAssuranceLevel.aal2);
        (await this.Client.ListFactors())!.Totp.Should().ContainSingle();
        await this.Client.Unenroll(new MfaUnenrollParams { FactorId = enrollment.Id });
        await this.Client.SignOut();
        await this.Client.SignIn(email, Password);
        var afterUnenroll = (await this.Client.GetAuthenticatorAssuranceLevel())!;
        afterUnenroll.CurrentLevel.Should().Be(AuthenticatorAssuranceLevel.aal1);
        afterUnenroll.NextLevel.Should().Be(AuthenticatorAssuranceLevel.aal1);
        (await this.Client.ListFactors())!.Totp.Should().BeEmpty();
    }
}
