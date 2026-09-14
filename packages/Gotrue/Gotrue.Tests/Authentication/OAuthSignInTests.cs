#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     End-to-end third-party (OAuth) sign-in against the live stack: the provider authorize URL the SDK
///     builds, the PKCE challenge it embeds, and linking a provider identity to an existing user.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class OAuthSignInTests : AuthClientFixture
{
    [TestMethod]
    public async Task SignIn_ShouldBuildProviderAuthorizeUrlWithoutState_GivenProvider()
    {
        // Provider-side state is owned by the GoTrue server; the SDK must not inject its own or
        // sign-in fails with bad_oauth_state (issue #377).
        var result = await this.Client.SignIn(Constants.Provider.Google);
        result.Uri.ToString().Should().StartWith($"{TestClients.CliAuthUrl}/authorize");
        result.Uri.Query.Should().Contain("provider=google").And.NotContain("state=");
    }

    [TestMethod]
    public async Task SignIn_ShouldIncludeScopesInAuthorizeUrl_GivenScopesOption()
    {
        var result = await this.Client.SignIn(Constants.Provider.Google, new SignInOptions { Scopes = "special scopes please" });
        result.Uri.Query.Should().Contain("provider=google").And.Contain("scopes=special+scopes+please").And.NotContain("state=");
    }

    [TestMethod]
    public async Task SignIn_ShouldReturnPkceVerifierAndChallengeUrl_GivenPkceFlow()
    {
        var result = await this.Client.SignIn(Constants.Provider.Github, new SignInOptions { FlowType = Constants.OAuthFlowType.PKCE });
        result.PKCEVerifier.Should().NotBeNullOrEmpty();
        result.Uri.Query.Should().Contain("flow_type=pkce")
            .And.Contain("code_challenge=")
            .And.Contain("code_challenge_method=s256")
            .And.Contain("provider=github");
    }

    [TestMethod]
    public async Task LinkIdentity_ShouldThrow_GivenNoSignedInUser()
    {
        var link = () => this.Client.LinkIdentity(Constants.Provider.Github, new SignInOptions { FlowType = Constants.OAuthFlowType.PKCE });
        await link.Should().ThrowAsync<GotrueException>("linking an identity requires an authenticated user");
    }

    [TestMethod]
    public async Task LinkIdentity_ShouldReturnPkceVerifier_GivenSignedInUser()
    {
        await this.SignUpNewUser();
        var result = await this.Client.LinkIdentity(Constants.Provider.Github, new SignInOptions { FlowType = Constants.OAuthFlowType.PKCE });
        result.PKCEVerifier.Should().NotBeNullOrEmpty();
    }
}
