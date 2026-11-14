#region

using System;
using System.Linq;
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
        await this.CreateOAuthClient();
        var session = await this.SignUpNewUser();
        var authorizationId = await this.StartAuthorizationRequest();

        var result = await this.Client.GetAuthorizationDetails(authorizationId);

        result.Details.Should().NotBeNull();
        result.Details!.AuthorizationId.Should().Be(authorizationId);
        result.Details.RedirectUri.Should().Be(this.RedirectUri);
        result.Details.Client.Id.Should().Be(this.ClientId);
        result.Details.User.Id.Should().Be(session.User!.Id);
        result.Details.User.Email.Should().Be(session.User.Email);
        result.Details.Scope.Should().Be("openid profile email");
        result.Redirect.Should().BeNull();
    }

    [TestMethod]
    public async Task ApproveAuthorization_ShouldReturnCodeRedirectAndCreateGrant_GivenConsentDetails()
    {
        await this.CreateOAuthClient();
        await this.SignUpNewUser();
        var authorizationId = await this.StartAuthorizationRequest();
        var details = await this.Client.GetAuthorizationDetails(authorizationId);
        details.Details.Should().NotBeNull();

        var redirect = await this.Client.ApproveAuthorization(authorizationId, new AuthorizationOptions { SkipBrowserRedirect = true });
        var parameters = ParseQuery(new Uri(redirect.RedirectUrl).Query);

        parameters.Should().ContainKey("code");
        parameters.Should().ContainKey("state");
        parameters["state"].Should().NotBeNullOrEmpty();
        var grants = await this.Client.ListGrants();
        grants.Should().ContainSingle(grant => grant.Client.Id == this.ClientId && grant.Scopes.Contains("openid"));
    }

    [TestMethod]
    public async Task GetAuthorizationDetails_ShouldReturnRedirect_GivenConsentWasPreviouslyGranted()
    {
        await this.CreateOAuthClient();
        await this.SignUpNewUser();

        var firstAuthorizationId = await this.StartAuthorizationRequest();
        await this.Client.GetAuthorizationDetails(firstAuthorizationId);
        await this.Client.ApproveAuthorization(firstAuthorizationId, new AuthorizationOptions { SkipBrowserRedirect = true });

        var nextAuthorizationId = await this.StartAuthorizationRequest();
        var result = await this.Client.GetAuthorizationDetails(nextAuthorizationId);

        result.Details.Should().BeNull();
        result.Redirect.Should().NotBeNull();
        var parameters = ParseQuery(new Uri(result.Redirect!.RedirectUrl).Query);
        parameters.Should().ContainKey("code");
        parameters.Should().ContainKey("state");
    }

    [TestMethod]
    public async Task DenyAuthorization_ShouldReturnAccessDeniedRedirect_GivenConsentDetails()
    {
        await this.CreateOAuthClient();
        await this.SignUpNewUser();
        var authorizationId = await this.StartAuthorizationRequest();
        var details = await this.Client.GetAuthorizationDetails(authorizationId);
        details.Details.Should().NotBeNull();

        var redirect = await this.Client.DenyAuthorization(authorizationId, new AuthorizationOptions { SkipBrowserRedirect = true });
        var parameters = ParseQuery(new Uri(redirect.RedirectUrl).Query);

        parameters.Should().ContainKey("error").WhoseValue.Should().Be("access_denied");
        parameters.Should().ContainKey("state");
        (await this.Client.ListGrants()).Should().BeEmpty();
    }

    [TestMethod]
    public async Task RevokeGrant_ShouldRemoveGrant_GivenUserHasApprovedClient()
    {
        await this.CreateOAuthClient();
        await this.SignUpNewUser();
        var authorizationId = await this.StartAuthorizationRequest();
        await this.Client.GetAuthorizationDetails(authorizationId);
        await this.Client.ApproveAuthorization(authorizationId, new AuthorizationOptions { SkipBrowserRedirect = true });
        (await this.Client.ListGrants()).Should().ContainSingle(grant => grant.Client.Id == this.ClientId);

        await this.Client.RevokeGrant(new RevokeGrantOptions { ClientId = this.ClientId });

        (await this.Client.ListGrants()).Should().NotContain(grant => grant.Client.Id == this.ClientId);
    }

    private static System.Collections.Generic.Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty);
}
