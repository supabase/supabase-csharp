#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Mfa;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Mfa;

#endregion

namespace Gotrue.Tests.UnenrollRecoveryCodes;

/// <summary>
///     End-to-end removal of the signed-in user's MFA recovery codes against the live stack. The local stack
///     cannot enroll recovery codes (the CLI does not expose the auth server's recovery codes enroll setting),
///     so these cover the rejections the stack can reach; a successful removal is pinned by
///     <see cref="UnenrollRecoveryCodesContractTests" />.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class UnenrollRecoveryCodesTests : MfaFixture
{
    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldThrowInsufficientAal_GivenAal1Session()
    {
        await this.SignUpNewUser();
        var unenrolling = () => this.Client.UnenrollRecoveryCodesAsync();
        (await unenrolling.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "DELETE /factors/recovery-codes requires an aal2 session (auth OpenAPI, 403 insufficient_aal)");
    }

    [TestMethod]
    public async Task UnenrollRecoveryCodesAsync_ShouldThrowFactorNotFound_GivenAal2SessionWithoutRecoveryCodes()
    {
        await this.SignUpNewUser();
        var enrollment = await this.EnrollTotp();
        await this.Client.ChallengeAndVerify(new MfaChallengeAndVerifyParams { FactorId = enrollment.Id, Code = TotpCode(enrollment) });
        var unenrolling = () => this.Client.UnenrollRecoveryCodesAsync();
        (await unenrolling.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_factor_not_found",
            "an aal2 user who never enrolled recovery codes has nothing to remove (auth OpenAPI, 404 mfa_factor_not_found)");
    }
}
