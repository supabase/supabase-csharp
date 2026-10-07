#region

using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Gotrue.Tests.TestUtils;

#endregion

namespace Gotrue.Tests.Transport;

/// <summary>
///     A 2xx body that isn't JSON throws a <see cref="GotrueException" /> carrying the status and the parse error.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class NonJsonResponseTests
{
    private const string ProxyErrorPage = "<html><body>502 Bad Gateway</body></html>";

    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitialize() => this.server = new MockGotrueServer();

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task SignIn_ShouldThrowGotrueException_GivenNonJsonBody()
    {
        this.MockProxyErrorPage("/token");
        var act = () => TestClients.Against(this.server).SignIn(RandomEmail(), Password);
        await act.Should().ThrowAsync<GotrueException>()
            .Where(exception => exception.StatusCode == 200 && exception.InnerException is JsonException,
                "auth-js surfaces a 2xx body it can't parse as an AuthRetryableFetchError, not a raw parse error");
    }

    [TestMethod]
    public async Task SignUp_ShouldThrowGotrueException_GivenNonJsonBody()
    {
        this.MockProxyErrorPage("/signup");
        var act = () => TestClients.Against(this.server).SignUp(RandomEmail(), Password);
        await act.Should().ThrowAsync<GotrueException>()
            .Where(exception => exception.StatusCode == 200 && exception.InnerException is JsonException,
                "sign-up reads the body outside the generic request path");
    }

    private void MockProxyErrorPage(string path) =>
        this.server.Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(ProxyErrorPage));
}
