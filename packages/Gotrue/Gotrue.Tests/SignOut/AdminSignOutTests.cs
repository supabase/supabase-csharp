#region

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Admin;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.SignOut;

/// <summary>
///     End-to-end admin sign-out against the live stack: each test signs up its own user, and the admin client ends
///     that user's sessions from their access token.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class AdminSignOutTests : AdminFixture
{
    [TestMethod]
    public async Task SignOutAsync_ShouldRevokeUserSession()
    {
        var accessToken = await SignUpUser();
        await this.Admin.SignOutAsync(accessToken);
        var lookup = () => this.Admin.GetUser(accessToken);
        await lookup.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "session_not_found",
                "the auth server deletes the token's session on a global sign-out, so the token no longer authenticates");
    }

    [TestMethod]
    public async Task SignOutAsync_ShouldThrowSessionNotFound_GivenSessionAlreadyRevoked()
    {
        var accessToken = await SignUpUser();
        await this.Admin.SignOutAsync(accessToken, SignOutScope.Local);
        var signOutAgain = () => this.Admin.SignOutAsync(accessToken);
        await signOutAgain.Should().ThrowAsync<GotrueException>()
            .Where(failure => failure.ErrorCode == "session_not_found",
                "the auth server answers 403 session_not_found to a token whose session was revoked");
    }

    private static async Task<string> SignUpUser()
    {
        var session = await TestClients.AgainstCliStack().SignUp(RandomEmail(), Password);
        return session?.AccessToken ?? throw new InvalidOperationException("The local stack returned no session on sign-up.");
    }
}
