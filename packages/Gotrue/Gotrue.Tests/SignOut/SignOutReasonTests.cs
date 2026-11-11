#region

using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Common;
using Supabase.Gotrue.Exceptions;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.SignOut;

/// <summary>
///     End-to-end sign-out reasons against the live stack: a listener reads from the client why the user was
///     signed out, telling an explicit sign-out apart from a session the server no longer accepts.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class SignOutReasonTests : AuthClientFixture
{
    private readonly List<SignOutReason?> reasons = new();

    [TestInitialize]
    public void RecordSignOutReasons() =>
        this.Client.AddStateChangedListener((sender, state) =>
        {
            if (state == SignedOut)
            {
                this.reasons.Add(sender.SignOutReason);
            }
        });

    [TestMethod]
    public async Task SignOut_ShouldReportUserInitiated()
    {
        await this.SignUpNewUser();
        await this.Client.SignOut();
        this.reasons.Should().Equal(new SignOutReason?[] { SignOutReason.UserInitiated },
            "the application asked to sign out (capability auth.session.sign_out_reason)");
    }

    [TestMethod]
    public async Task RefreshToken_ShouldReportSessionExpired_GivenTheServerRevokedTheSession()
    {
        var session = await this.SignUpNewUser();
        await RevokeOnTheServer(session.AccessToken!);
        var refresh = () => this.Client.RefreshToken();
        await refresh.Should().ThrowAsync<GotrueException>();
        this.reasons.Should().Equal(new SignOutReason?[] { SignOutReason.SessionExpired },
            "the server rejected the refresh token, so the session ended without the user asking (capability auth.session.sign_out_reason)");
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
