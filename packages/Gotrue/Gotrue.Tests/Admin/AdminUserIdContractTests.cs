#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Mfa;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     Pins that a caller-supplied user or factor id stays one segment of the admin request path. Placed raw,
///     an id such as <c>x/../../users/{other}</c> is resolved by <see cref="System.Uri" /> into another route.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class AdminUserIdContractTests
{
    private const string ServiceKey = "service-role-jwt";
    private const string TraversingId = "x/../../users/victim";
    private const string EscapedTraversingId = "x%2F..%2F..%2Fusers%2Fvictim";

    private MockGotrueServer server = null!;
    private AdminClient admin = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
        this.admin = new AdminClient(ServiceKey, new ClientOptions { Url = this.server.Url });
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task GetUserById_ShouldKeepIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.admin.GetUserById(TraversingId);
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/admin/users/{EscapedTraversingId}");
    }

    [TestMethod]
    public async Task UpdateUserById_ShouldKeepIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.admin.UpdateUserById(TraversingId, new AdminUserAttributes());
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/admin/users/{EscapedTraversingId}");
    }

    [TestMethod]
    public async Task DeleteUser_ShouldKeepIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.admin.DeleteUser(TraversingId);
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/admin/users/{EscapedTraversingId}");
    }

    [TestMethod]
    public async Task ListFactors_ShouldKeepUserIdInOneSegment_GivenIdWithPathSeparators()
    {
        this.server.Given(Request.Create().WithPath(new WildcardMatcher("*/factors")).UsingGet()).AtPriority(0)
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("[]"));
        await this.admin.ListFactors(new MfaAdminListFactorsParams { UserId = TraversingId });
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/admin/users/{EscapedTraversingId}/factors");
    }

    [TestMethod]
    public async Task DeleteFactor_ShouldKeepUserIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.admin.DeleteFactor(new MfaAdminDeleteFactorParams { UserId = TraversingId, Id = "factor-1" });
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/admin/users/{EscapedTraversingId}/factors/factor-1");
    }

    [TestMethod]
    public async Task DeleteFactor_ShouldKeepFactorIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.admin.DeleteFactor(new MfaAdminDeleteFactorParams { UserId = "user-1", Id = TraversingId });
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/admin/users/user-1/factors/{EscapedTraversingId}");
    }
}
