#region

using System.Net.Http;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.StatelessClient;

#endregion

namespace Gotrue.Tests.Stateless;

/// <summary>
///     Pins the soft-delete wire contract for the stateless admin delete-user request (gotrue-js parity):
///     <c>should_soft_delete</c> reaches the body and defaults to a hard delete.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StatelessDeleteUserSoftDeleteContractTests
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
        var options = new StatelessClientOptions { Url = this.server.Url };
        await new StatelessClient().DeleteUser(UserId, ServiceKey, options, shouldSoftDelete: true);
        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Delete)
            .WithBooleanJsonBody("should_soft_delete", true);
    }
}
