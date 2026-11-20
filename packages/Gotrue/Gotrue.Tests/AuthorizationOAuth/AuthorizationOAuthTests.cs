#region

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.AuthorizationOAuth;

#endregion

namespace Gotrue.Tests.AuthorizationOAuth;

/// <summary>Exercises OAuth authorization and grant management against the local Supabase stack.</summary>
[TestClass]
[TestCategory("E2E")]
public class AuthorizationOAuthTests : AuthorizationOAuthFixture
{
    [TestMethod]
    public async Task GetAuthorizationDetails_ShouldReturnConsentDetails_GivenNewAuthorization()
    {
        var authorizationId = DetailsAuthorizationId;
        var session = await this.SignInSeededUser();

        var result = await this.Client.GetAuthorizationDetails(authorizationId);

        result.Details.Should().NotBeNull();
        result.Details!.AuthorizationId.Should().Be(authorizationId);
        result.Details.RedirectUri.Should().Be("http://localhost:3000/callback");
        result.Details.Client.Id.Should().Be(DetailsClientId);
        result.Details.Client.Name.Should().Be("Mock OAuth Client 009");
        result.Details.User.Id.Should().Be(session!.User!.Id);
        result.Details.User.Email.Should().Be(session.User.Email);
        result.Details.Scope.Should().Be("openid email profile");
        result.Redirect.Should().BeNull();
    }

    [TestMethod]
    public async Task GetAuthorizationDetails_ShouldReturnRedirect_GivenConsentWasAlreadyGranted()
    {
        await this.SignInSeededUser();

        var result = await this.Client.GetAuthorizationDetails(AutoApprovedAuthorizationId);

        result.Details.Should().BeNull();
        result.Redirect.Should().NotBeNull();
        result.Redirect!.RedirectUrl.Should().StartWith("http://localhost:3000/callback?")
            .And.Contain("code=")
            .And.Contain("state=test-state-004");
    }

    [TestMethod]
    public async Task ApproveAuthorization_ShouldReturnCodeRedirect_GivenConsentDetails()
    {
        await this.SignInSeededUser();
        var details = await this.Client.GetAuthorizationDetails(ApprovalAuthorizationId);
        details.Details.Should().NotBeNull();

        var redirect = await this.Client.ApproveAuthorization(ApprovalAuthorizationId);

        redirect.RedirectUrl.Should().StartWith("http://localhost:3000/callback?")
            .And.Contain("code=")
            .And.Contain("state=test-state-002");
    }

    [TestMethod]
    public async Task DenyAuthorization_ShouldReturnAccessDeniedRedirect_GivenConsentDetails()
    {
        await this.SignInSeededUser();
        var details = await this.Client.GetAuthorizationDetails(DenialAuthorizationId);
        details.Details.Should().NotBeNull();

        var redirect = await this.Client.DenyAuthorization(DenialAuthorizationId);

        redirect.RedirectUrl.Should().StartWith("http://localhost:3000/callback?")
            .And.Contain("error=access_denied")
            .And.Contain("state=test-state-003");
    }

    [TestMethod]
    public async Task ListGrants_ShouldReturnSeededGrant_GivenUserHasAuthorizedClient()
    {
        await this.SignInSeededUser();

        var grants = await this.Client.ListGrants();

        var grant = grants.Should().ContainSingle(item => item.Client.Id == ListGrantsClientId).Which;
        grant.Client.Name.Should().Be("Mock OAuth Client For Listing");
        grant.Client.Uri.Should().Be("http://localhost:3000");
        grant.Client.LogoUri.Should().Be("http://localhost:3000/logo.png");
        grant.Scopes.Should().BeEquivalentTo("openid", "email", "profile");
    }

    [TestMethod]
    public async Task ListGrants_ShouldReturnNoGrants_GivenNewUserHasNotAuthorizedClients()
    {
        await this.SignUpNewUser();

        var grants = await this.Client.ListGrants();

        grants.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RevokeGrant_ShouldRemoveSeededGrant_GivenUserHasAuthorizedClient()
    {
        await this.SignInSeededUser();
        (await this.Client.ListGrants()).Should().ContainSingle(grant => grant.Client.Id == RevokeGrantClientId);

        await this.Client.RevokeGrant(new RevokeGrantOptions { ClientId = RevokeGrantClientId });

        (await this.Client.ListGrants()).Should().NotContain(grant => grant.Client.Id == RevokeGrantClientId);
    }
}
