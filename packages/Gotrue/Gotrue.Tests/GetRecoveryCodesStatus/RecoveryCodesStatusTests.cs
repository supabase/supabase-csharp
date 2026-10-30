#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.GetRecoveryCodesStatus;

/// <summary>
///     End-to-end reading of the signed-in user's MFA recovery codes status against the live stack. The local
///     stack cannot enroll recovery codes (the CLI does not expose the auth server's recovery codes enroll
///     setting), so these cover the not-enrolled and anonymous answers; the enrolled status is pinned by
///     <see cref="RecoveryCodesStatusContractTests" />.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class RecoveryCodesStatusTests : AuthClientFixture
{
    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldReturnNull_GivenUserWithoutRecoveryCodes()
    {
        await this.SignUpNewUser();
        var status = await this.Client.GetRecoveryCodesStatusAsync();
        status.Should().BeNull("a freshly signed-up user has not enrolled recovery codes (GET /factors/recovery-codes answers mfa_factor_not_found)");
    }

    [TestMethod]
    public async Task GetRecoveryCodesStatusAsync_ShouldThrowNoAuthorization_GivenAnonymousUser()
    {
        await this.Client.SignInAnonymously();
        var reading = () => this.Client.GetRecoveryCodesStatusAsync();
        (await reading.Should().ThrowAsync<GotrueException>()).Which.ErrorCode.Should().Be("no_authorization",
            "anonymous users cannot use recovery codes (auth OpenAPI, GET /factors/recovery-codes 403)");
    }
}
