#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.AuthorizationOAuth;

#endregion

namespace Gotrue.Tests.AuthorizationOAuth;

/// <summary>Checks OAuth authorization response mapping and the request payloads.</summary>
[TestClass]
[TestCategory("Contract")]
public class AuthorizationOAuthContractTests : RequestApprovalFixture
{
    private const string AccessToken = "an-access-token";

    [TestMethod]
    public async Task ApproveAuthorization_ShouldSendTheAction_GivenAuthorizationId()
    {
        await this.Api.ApproveAuthorization(AccessToken, "authorization-id");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task DenyAuthorization_ShouldSendTheAction_GivenAuthorizationId()
    {
        await this.Api.DenyAuthorization(
            AccessToken,
            "authorization-id",
            new AuthorizationOptions { SkipBrowserRedirect = true }
        );
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task GetAuthorizationDetails_ShouldReturnRedirect_GivenConsentWasAlreadyGranted()
    {
        this.RespondTo("/oauth/authorizations/authorization-id", Fixture("authorization_redirect.json"));

        var response = await this.Api.GetAuthorizationDetails(AccessToken, "authorization-id");

        response.Redirect.Should().BeEquivalentTo(new OAuthRedirect
        {
            RedirectUrl = "https://app.example/callback?code=auth-code&state=state",
        });
        response.Details.Should().BeNull();
    }

    [TestMethod]
    public async Task GetAuthorizationDetails_ShouldReturnDetails_GivenConsentIsRequired()
    {
        this.RespondTo("/oauth/authorizations/authorization-id", Fixture("authorization_details.json"));

        var response = await this.Api.GetAuthorizationDetails(AccessToken, "authorization-id");

        response.Details.Should().BeEquivalentTo(new OAuthAuthorizationDetails
        {
            AuthorizationId = "authorization-id",
            RedirectUri = "https://app.example/callback",
            Client = new OAuthAuthorizationClient
            {
                Id = "client-id",
                Name = "Example app",
                Uri = "https://app.example",
                LogoUri = "https://app.example/logo.png",
            },
            User = new OAuthAuthorizationUser { Id = "user-id", Email = "user@example.com" },
            Scope = "openid profile email",
        });
        response.Redirect.Should().BeNull();
    }

    [TestMethod]
    public async Task ListGrants_ShouldReturnGrantsWithoutClientIdQueryParameter()
    {
        this.RespondTo("/user/oauth/grants", Fixture("oauth_grants.json"));

        var grants = await this.Api.ListGrants(AccessToken);

        grants.Should().BeEquivalentTo(new[]
        {
            new OAuthGrant
            {
                Client = new OAuthAuthorizationClient
                {
                    Id = "client-id",
                    Name = "Example app",
                    Uri = "https://app.example",
                    LogoUri = "https://app.example/logo.png",
                },
                Scopes = new List<string> { "openid", "profile" },
                GrantedAt = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero),
            },
        });
        this.ValidateNoQueryParameter("client_id");
    }

    [TestMethod]
    public async Task RevokeGrant_ShouldSendTheClientIdAsAQueryParameter()
    {
        await this.Api.RevokeGrant(AccessToken, new RevokeGrantOptions { ClientId = "client-id" });

        this.ValidateQueryParameters("client_id", "client-id");
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AuthorizationOAuth", "Data", name));
}
