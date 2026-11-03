#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.RegenerateRecoveryCodes;

/// <summary>
///     End-to-end regeneration of the signed-in user's MFA recovery codes against the live stack. The local stack
///     cannot enroll recovery codes (the CLI does not expose the auth server's recovery codes enroll setting, which
///     defaults to off) and checks that setting before the session's aal or the user's codes, so these cover the
///     rejections the stack can reach; a successful regeneration and the aal and not-enrolled rejections are pinned by
///     <see cref="RegenerateRecoveryCodesContractTests" />.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RegenerateRecoveryCodesTests : AuthClientFixture
{
    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowEnrollNotEnabled_GivenServerWithRecoveryCodesDisabled()
    {
        await this.SignUpNewUser();
        var regenerating = () => this.Client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_recovery_codes_enroll_not_enabled",
            "the auth server leaves GOTRUE_MFA_RECOVERY_CODES_ENROLL_ENABLED off by default and refuses regeneration before checking the session's aal (auth OpenAPI, POST /factors/recovery-codes/regenerate 422)");
    }

    [TestMethod]
    public async Task RegenerateRecoveryCodesAsync_ShouldThrowNoAuthorization_GivenAnonymousUser()
    {
        await this.Client.SignInAnonymously();
        var regenerating = () => this.Client.RegenerateRecoveryCodesAsync();
        (await regenerating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("no_authorization",
            "anonymous users cannot use recovery codes (auth OpenAPI, POST /factors/recovery-codes/regenerate 403)");
    }
}
