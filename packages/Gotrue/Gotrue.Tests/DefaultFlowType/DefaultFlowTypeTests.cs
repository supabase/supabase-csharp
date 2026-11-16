#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.DefaultFlowType;

/// <summary>
///     End-to-end PKCE through the client's default flow type, against the live stack: a client whose options choose
///     PKCE completes a password reset whose call never mentions a flow type, reading the link from the stack's local
///     mail server (client.authentication_integration.oauth_flow_type).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class DefaultFlowTypeTests
{
    [TestMethod]
    public async Task ExchangeCodeForSession_ShouldCompleteAPasswordReset_GivenAPkceClientAndACallThatLeavesTheFlowTypeUnset()
    {
        var client = TestClients.AgainstCliStackWithDefaultFlowType(OAuthFlowType.PKCE);
        var email = await SignUpThenSignOut(client);
        var reset = await client.ResetPasswordForEmail(new ResetPasswordForEmailOptions(email));
        var session = await client.ExchangeCodeForSession(reset.PKCEVerifier!, await Mailbox.ClickForAuthCodeAsync(email));
        session!.User!.Email.Should().Be(email, "the reset started as PKCE from the client's default, so its code exchanges for the user's session (RFC 7636 §4.5)");
    }

    private static async Task<string> SignUpThenSignOut(IGotrueClient<User, Session> client)
    {
        var email = RandomEmail();
        await client.SignUpAsync(SignUpType.Email, email, Password, new SignUpOptions { FlowType = OAuthFlowType.Implicit });
        await client.SignOut();
        return email;
    }
}
