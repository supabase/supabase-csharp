#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.GenerateRecoveryCodes;

/// <summary>
///     End-to-end generation of the signed-in user's MFA recovery codes against the live stack. The local stack
///     cannot enroll recovery codes (the CLI does not expose the auth server's recovery codes enroll setting, which
///     defaults to off), so these cover the rejections the stack can reach; a successful generation is pinned by
///     <see cref="GenerateRecoveryCodesContractTests" />.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class GenerateRecoveryCodesTests : AuthClientFixture
{
    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrowEnrollNotEnabled_GivenServerWithRecoveryCodesDisabled()
    {
        await this.SignUpNewUser();
        var generating = () => this.Client.GenerateRecoveryCodesAsync();
        (await generating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_recovery_codes_enroll_not_enabled",
            "the auth server leaves GOTRUE_MFA_RECOVERY_CODES_ENROLL_ENABLED off by default (auth OpenAPI, POST /factors/recovery-codes 422)");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesAsync_ShouldThrowNoAuthorization_GivenAnonymousUser()
    {
        await this.Client.SignInAnonymously();
        var generating = () => this.Client.GenerateRecoveryCodesAsync();
        (await generating.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("no_authorization",
            "anonymous users cannot use recovery codes (auth OpenAPI, POST /factors/recovery-codes 403)");
    }
}
