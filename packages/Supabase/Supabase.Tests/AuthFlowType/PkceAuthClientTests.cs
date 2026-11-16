using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;

namespace Supabase.Tests.AuthFlowType;

/// <summary>
/// A Supabase client configured with <see cref="SupabaseOptions.AuthFlowType"/> set to PKCE, against a live Supabase
/// CLI stack (<c>supabase start</c>, localhost:54321): Auth starts a PKCE password reset the server accepts, although
/// the call never mentions a flow type (client.authentication_integration.oauth_flow_type).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class PkceAuthClientTests
{
    private const string PublishableKey = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldReturnAVerifier_GivenAPkceClientAndACallThatLeavesTheFlowTypeUnset()
    {
        var client = new Client("http://127.0.0.1:54321", PublishableKey, new SupabaseOptions { AuthFlowType = OAuthFlowType.PKCE });
        var reset = await client.Auth.ResetPasswordForEmail(new ResetPasswordForEmailOptions($"{Guid.NewGuid():N}@example.com"));
        reset.PKCEVerifier.Should().NotBeNullOrEmpty("the server accepted a password reset started as PKCE from the client's default (RFC 7636 §4.3)");
    }
}
