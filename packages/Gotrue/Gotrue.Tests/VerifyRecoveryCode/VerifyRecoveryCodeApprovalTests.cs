#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Gotrue.Tests.VerifyRecoveryCode;

/// <summary>
///     Pins the exact bytes a recovery code verification puts on the wire: the code exactly as the user typed it,
///     since the server normalizes letter case, whitespace and separators itself.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class VerifyRecoveryCodeApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task VerifyRecoveryCodeRequest_ShouldSendTheCodeAsTyped()
    {
        await this.Api.VerifyRecoveryCodeAsync("user-jwt", "K4M9-X7QP-2AB8-HT3Z");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
