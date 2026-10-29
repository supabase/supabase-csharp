#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Mfa;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Mfa;

/// <summary>
///     Pins that a caller-supplied factor id stays one segment of the MFA request path, so it cannot steer a
///     challenge, verification or unenrollment to another route.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class MfaFactorIdContractTests
{
    private const string AccessToken = "access-token";
    private const string TraversingId = "x/../../factors/victim";
    private const string EscapedTraversingId = "x%2F..%2F..%2Ffactors%2Fvictim";

    private MockGotrueServer server = null!;
    private Api api = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
        this.api = new Api(this.server.Url);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task Challenge_ShouldKeepFactorIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.api.Challenge(AccessToken, new MfaChallengeParams { FactorId = TraversingId });
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/factors/{EscapedTraversingId}/challenge");
    }

    [TestMethod]
    public async Task Verify_ShouldKeepFactorIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.api.Verify(AccessToken, new MfaVerifyParams { FactorId = TraversingId, ChallengeId = "challenge-1", Code = "123456" });
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/factors/{EscapedTraversingId}/verify");
    }

    [TestMethod]
    public async Task Unenroll_ShouldKeepFactorIdInOneSegment_GivenIdWithPathSeparators()
    {
        await this.api.Unenroll(AccessToken, new MfaUnenrollParams { FactorId = TraversingId });
        this.server.VerifySingleReceivedRequest().WithEncodedPath($"/factors/{EscapedTraversingId}");
    }
}
