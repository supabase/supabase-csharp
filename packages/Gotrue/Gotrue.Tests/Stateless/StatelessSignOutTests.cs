#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Supabase.Gotrue.Constants;
using static Supabase.Gotrue.StatelessClient;

#endregion

namespace Gotrue.Tests.Stateless;

/// <summary>
///     Stateless sign-out sends the requested scope to <c>/logout</c>.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StatelessSignOutTests
{
    [TestMethod]
    public async Task SignOutAsync_ShouldSendTheRequestedScope()
    {
        using var server = new MockGotrueServer();
        server.Given(Request.Create().WithPath("/logout").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        var options = new StatelessClientOptions { Url = server.Url };
        await new StatelessClient().SignOutAsync("user-access-token", options, SignOutScope.Local);
        server.VerifySingleReceivedRequest().WithQueryParam("scope", "local");
    }
}
