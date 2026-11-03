#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Mfa;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Mfa;

#endregion

namespace Gotrue.Tests.RegenerateRecoveryCodes;

/// <summary>
///     End-to-end regeneration of the signed-in user's MFA recovery codes against the live stack. The local stack
///     cannot enroll recovery codes (the CLI does not expose the auth server's recovery codes enroll setting, which
///     defaults to off), so these cover the rejections the stack can reach; a successful regeneration is pinned by
///     <see cref="RegenerateRecoveryCodesContractTests" />.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RegenerateRecoveryCodesTests : MfaFixture
{
    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowInsufficientAal_GivenAal1Session()
    {
        await this.SignUpNewUser();
        var regenerating = () => this.Client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("insufficient_aal",
            "POST /factors/recovery-codes/regenerate requires an aal2 session (auth OpenAPI, 403 insufficient_aal)");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowNoAuthorization_GivenAnonymousUser()
    {
        await this.Client.SignInAnonymously();
        var regenerating = () => this.Client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("no_authorization",
            "anonymous users cannot use recovery codes (auth OpenAPI, POST /factors/recovery-codes/regenerate 403)");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowFactorNotFound_GivenAal2SessionWithoutRecoveryCodes()
    {
        await this.SignUpNewUser();
        var enrollment = await this.EnrollTotp();
        await this.Client.ChallengeAndVerify(new MfaChallengeAndVerifyParams { FactorId = enrollment.Id, Code = TotpCode(enrollment) });
        var regenerating = () => this.Client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_factor_not_found",
            "an aal2 user who never enrolled recovery codes has no set to replace (auth OpenAPI, 404 mfa_factor_not_found)");
    }
}
