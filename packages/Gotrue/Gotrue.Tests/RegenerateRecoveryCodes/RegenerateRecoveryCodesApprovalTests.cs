#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Gotrue.Tests.RegenerateRecoveryCodes;

/// <summary>
///     Pins the exact bytes a recovery codes regeneration puts on the wire: no body at all, since the endpoint takes
///     no request body.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class RegenerateRecoveryCodesApprovalTests : RequestApprovalFixture
{
    private const string RegeneratedCodes = """{"id":"2b306a77-21dc-4110-ba71-537cb56b9e98","friendly_name":"Recovery codes","codes":["7tq3m8wkx2pn9hra"]}""";

    [TestInitialize]
    public void AnswerWithRegeneratedCodes() => this.RespondTo("/factors/recovery-codes/regenerate", RegeneratedCodes);

    [TestMethod]
    public async Task RegenerateRecoveryCodesRequest_ShouldSendNoBody()
    {
        await this.Api.RegenerateRecoveryCodesAsync("user-jwt");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
