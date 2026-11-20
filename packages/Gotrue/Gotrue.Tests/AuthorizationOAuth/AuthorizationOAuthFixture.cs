using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Gotrue.Tests.Support;

namespace Gotrue.Tests.AuthorizationOAuth;

/// <summary>Base fixture for OAuth authorization end-to-end tests against the local Supabase stack.</summary>
public abstract class AuthorizationOAuthFixture : AuthClientFixture
{
    protected const string SeedUserEmail = "ynnxnf73bycm@supabase.io";
    protected const string DetailsAuthorizationId = "test-authorization-id-001";
    protected const string ApprovalAuthorizationId = "test-authorization-id-002";
    protected const string DenialAuthorizationId = "test-authorization-id-003";
    protected const string AutoApprovedAuthorizationId = "test-authorization-id-004";
    protected const string DetailsClientId = "aaaaaaaa-0000-0000-0000-000000000000";
    protected const string AutoApprovedClientId = "bbbbbbbb-0000-0000-0000-000000000000";
    protected const string ListGrantsClientId = "cccccccc-0000-0000-0000-000000000000";
    protected const string RevokeGrantClientId = "dddddddd-0000-0000-0000-000000000000";

    [TestInitialize]
    public void ConfigureOAuthOrigin() => this.Client.Options.Headers["Origin"] = "http://127.0.0.1:3000";

    protected Task<Session?> SignInSeededUser() => this.Client.SignIn(SeedUserEmail, TestUtils.Password);
}
