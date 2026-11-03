#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.VerifyRecoveryCode;

/// <summary>
///     End-to-end verification of an MFA recovery code against the live stack. The local stack cannot verify
///     recovery codes (the CLI does not expose the auth server's recovery codes verify setting, which defaults to
///     off), so these cover the rejections the stack can reach; a successful verification is pinned by
///     <see cref="VerifyRecoveryCodeContractTests" />.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class VerifyRecoveryCodeTests : AuthClientFixture
{
    private const string RecoveryCode = "K4M9-X7QP-2AB8-HT3Z";

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowVerifyNotEnabled_GivenServerWithRecoveryCodesDisabled()
    {
        await this.SignUpNewUser();
        var verifying = () => this.Client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("mfa_recovery_codes_verify_not_enabled",
            "the auth server leaves GOTRUE_MFA_RECOVERY_CODES_VERIFY_ENABLED off by default (auth OpenAPI, POST /factors/recovery-codes/verify 422)");
    }

    [TestMethod]
    public async Task VerifyRecoveryCodeAsync_ShouldThrowNoAuthorization_GivenAnonymousUser()
    {
        await this.Client.SignInAnonymously();
        var verifying = () => this.Client.VerifyRecoveryCodeAsync(RecoveryCode);
        (await verifying.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("no_authorization",
            "anonymous users cannot use recovery codes (auth OpenAPI, POST /factors/recovery-codes/verify 403)");
    }
}
