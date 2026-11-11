#region

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Exceptions;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     End-to-end sign-out against the live stack: signing out clears the current session and user, a subsequent
///     sign-up establishes a distinct identity, and a global sign-out the server refuses keeps the session.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SignOutTests : AuthClientFixture
{
    [TestMethod]
    public async Task SignOut_ShouldClearSessionAndUser()
    {
        await this.SignUpNewUser();
        this.StateChanges.Should().Contain(SignedIn);
        this.StateChanges.Clear();
        await this.Client.SignOut();
        this.VerifySignedOut();
    }

    [TestMethod]
    public async Task SignUp_ShouldEstablishDistinctIdentity_GivenPriorSignOut()
    {
        var firstUser = await this.SignUpNewUser();
        this.StateChanges.Clear();
        await this.Client.SignOut();
        this.VerifySignedOut();
        this.StateChanges.Clear();
        var secondUser = await this.SignUpNewUser();
        this.StateChanges.Should().Contain(SignedIn);
        secondUser.User!.Id.Should().NotBe(firstUser.User!.Id);
    }

    [TestMethod]
    public async Task SignOut_ShouldKeepTheLocalSession_GivenTheServerNoLongerKnowsTheSession()
    {
        var session = await this.SignUpNewUser();
        await RevokeOnTheServer(session.AccessToken!);
        var signOut = () => this.Client.SignOut();
        await signOut.Should().ThrowAsync<GotrueException>();
        this.Client.CurrentSession.Should().NotBeNull(
            "a global sign-out the server refused keeps the session, so the caller can fall back to a local sign-out (spec auth.session.sign_out)");
    }

    private static async Task RevokeOnTheServer(string accessToken)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestClients.CliAuthUrl}/logout?scope=global");
        request.Headers.Add("apikey", TestClients.CliAnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
