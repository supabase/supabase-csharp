#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.GenerateRecoveryCodes;

#endregion

namespace Gotrue.Tests.GenerateRecoveryCodes;

/// <summary>
///     Pins the exact bytes a recovery codes generation puts on the wire: the friendly name when one is given, and no
///     body at all otherwise, since the server treats the body as optional.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class GenerateRecoveryCodesApprovalTests : RequestApprovalFixture
{
    private const string GeneratedCodes = """{"id":"2b306a77-21dc-4110-ba71-537cb56b9e98","friendly_name":"Recovery codes","codes":["k4m9x7qp2ab8ht3z"]}""";

    [TestInitialize]
    public void AnswerWithGeneratedCodes() => this.RespondTo("/factors/recovery-codes", GeneratedCodes);

    [TestMethod]
    public async Task GenerateRecoveryCodesRequest_ShouldSendTheFriendlyName_GivenFriendlyName()
    {
        await this.Api.GenerateRecoveryCodesAsync("user-jwt", new GenerateRecoveryCodesOptions { FriendlyName = "Backup codes" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesRequest_ShouldSendNoBody_GivenNoOptions()
    {
        await this.Api.GenerateRecoveryCodesAsync("user-jwt");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task GenerateRecoveryCodesRequest_ShouldSendNoBody_GivenOptionsWithoutFriendlyName()
    {
        await this.Api.GenerateRecoveryCodesAsync("user-jwt", new GenerateRecoveryCodesOptions());
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
