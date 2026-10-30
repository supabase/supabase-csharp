#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.UpdateFactor;

#endregion

namespace Gotrue.Tests.UpdateFactor;

/// <summary>
///     Pins the exact bytes a factor update puts on the wire: only the members set, every member when all are set, and
///     an empty object when none is.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class UpdateFactorApprovalTests : RequestApprovalFixture
{
    private const string UserId = "8d3f1c2a-5b6e-4f70-9a81-b2c3d4e5f607";

    private const string FactorId = "2b306a77-21dc-4110-ba71-537cb56b9e98";

    [TestMethod]
    public async Task UpdateFactorRequest_ShouldSendOnlyTheFriendlyName_GivenFriendlyNameOnly()
    {
        await this.Api.UpdateFactorAsync("service-jwt", UserId, FactorId, new FactorChanges { FriendlyName = "Work phone" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateFactorRequest_ShouldSendEveryChange_GivenEveryMember()
    {
        await this.Api.UpdateFactorAsync("service-jwt", UserId, FactorId, new FactorChanges { FriendlyName = "Work phone", Phone = "+15555550199" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task UpdateFactorRequest_ShouldSendEmptyObject_GivenNoChange()
    {
        await this.Api.UpdateFactorAsync("service-jwt", UserId, FactorId, new FactorChanges());
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
