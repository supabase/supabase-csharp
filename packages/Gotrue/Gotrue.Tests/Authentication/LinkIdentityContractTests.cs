#region

using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Pins the request LinkIdentity puts on the wire for the OAuth (PKCE) flow: it must ask GoTrue for the
///     provider URL in the response body rather than take a 302.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class LinkIdentityContractTests
{
    private const string AuthorizeResponse =
        """
        { "url": "https://provider.example.com/authorize?client_id=abc" }
        """;

    private IGotrueApi<User, Session> api = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.api = new Api(this.server.Url);
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    public async Task LinkIdentity_ShouldSkipTheHttpRedirect_GivenProvider()
    {
        // Issue #378: without skip_http_redirect HttpClient follows GoTrue's 302 and drops the
        // Authorization/apikey headers on the cross-domain hop, so Kong rejects the request with
        // "No API key found in request".
        this.server.Given(Request.Create().WithPath("/user/identities/authorize").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(AuthorizeResponse));

        var state = await this.api.LinkIdentity("user-access-token", Constants.Provider.Google,
            new SignInOptions { FlowType = Constants.OAuthFlowType.PKCE });

        this.server.VerifySingleReceivedRequest()
            .WithMethod(HttpMethod.Get)
            .WithPath("/user/identities/authorize")
            .WithQueryParam("skip_http_redirect", "true")
            .WithQueryParam("provider", "google")
            .WithHeader("Authorization", "Bearer user-access-token");

        state.Uri.ToString().Should().Be("https://provider.example.com/authorize?client_id=abc",
            "the provider URL returned in the body replaces the one the SDK would have been redirected to");
    }

    [TestMethod]
    public async Task LinkIdentity_ShouldThrow_GivenResponseWithoutProviderUrl()
    {
        this.server.Given(Request.Create().WithPath("/user/identities/authorize").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("{}"));

        var link = () => this.api.LinkIdentity("user-access-token", Constants.Provider.Google,
            new SignInOptions { FlowType = Constants.OAuthFlowType.PKCE });

        await link.Should().ThrowAsync<GotrueException>("returning the un-redirected SDK-built url would silently send the caller to an endpoint that rejects the request");
    }
}
