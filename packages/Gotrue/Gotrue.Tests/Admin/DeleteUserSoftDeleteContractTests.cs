#region

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Admin;

/// <summary>
///     Pins the soft-delete wire contract for the admin delete-user request. GoTrue's admin delete supports
///     <c>should_soft_delete</c> (gotrue-js parity); the SDK sends it on every delete, defaulting to a hard
///     delete so existing behavior is preserved.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class DeleteUserSoftDeleteContractTests
{
    private const string ServiceKey = "service-role-jwt";
    private const string UserId = "user-123";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        this.server = new MockGotrueServer();
        this.server.Given(Request.Create().WithPath($"/admin/users/{UserId}").UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task DeleteUser_ShouldSendShouldSoftDeleteTrue_GivenSoftDeleteRequested()
    {
        var admin = new AdminClient(ServiceKey, this.Options());
        await admin.DeleteUser(UserId, shouldSoftDelete: true);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithBooleanJsonBody("should_soft_delete", true);
    }

    [TestMethod]
    public async Task DeleteUser_ShouldSendShouldSoftDeleteFalse_GivenNoSoftDeleteRequested()
    {
        var admin = new AdminClient(ServiceKey, this.Options());
        await admin.DeleteUser(UserId);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithBooleanJsonBody("should_soft_delete", false);
    }

    private ClientOptions Options() => new()
    {
        Url = this.server.Url,
        Headers = new Dictionary<string, string> { ["apikey"] = "project-key" },
    };
}
