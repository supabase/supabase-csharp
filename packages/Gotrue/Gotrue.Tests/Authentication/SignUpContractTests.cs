#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants.AuthState;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Pins how SignUp treats the session the server returns. With email confirmations disabled the user is
///     auto-confirmed via <c>email_confirmed_at</c> (not <c>confirmed_at</c>), and the client must adopt that
///     session even though <c>AllowUnconfirmedUserSessions</c> is left at its default of false (issue #130).
///     Pending signups return the user without adopting a session unless that option is enabled (issue #259).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignUpContractTests
{
    // A signup response for an auto-confirmed user: a usable session whose user is confirmed through
    // email_confirmed_at with confirmed_at absent — the shape a stack with enable_confirmations = false returns.
    private const string AutoConfirmedSignUp =
        """
        {
          "access_token": "auto-confirmed-access-token",
          "refresh_token": "auto-confirmed-refresh-token",
          "token_type": "bearer",
          "expires_in": 3600,
          "user": {
            "id": "user-id-130",
            "aud": "authenticated",
            "email": "auto@example.com",
            "email_confirmed_at": "2026-06-26T06:08:11Z"
          }
        }
        """;

    private const string PendingConfirmationSignUp =
        """
        { "id": "user-id-259", "aud": "authenticated", "email": "pending@example.com", "confirmation_sent_at": "2026-06-09T09:15:57Z" }
        """;

    private const string PendingPhoneSignUp =
        """
        { "id": "user-id-259-phone", "aud": "authenticated", "phone": "15555550123", "confirmation_sent_at": "2026-06-09T09:15:57Z" }
        """;

    private const string Acknowledgement =
        """
        { "msg": "Confirmation link accepted. Please proceed to confirm link sent to the other email", "code": 200 }
        """;

    private readonly List<Constants.AuthState> stateChanges = new();
    private IGotrueClient<User, Session> client = null!;
    private MockGotrueServer server = null!;

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        this.client.AddStateChangedListener((_, state) => this.stateChanges.Add(state));
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    private void StubSignUp(string body) =>
        this.server.Given(Request.Create().WithPath("/signup").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

    [TestMethod]
    public async Task SignUp_ShouldAdoptTheSession_GivenAutoConfirmedUserAndDefaultOptions()
    {
        this.StubSignUp(AutoConfirmedSignUp);
        await this.client.SignUp(RandomEmail(), Password);
        using (new AssertionScope())
        {
            this.client.CurrentSession.Should().NotBeNull(
                "an auto-confirmed signup returns a usable session the client must adopt even with AllowUnconfirmedUserSessions off (issue #130)");
            this.client.CurrentUser!.Id.Should().Be("user-id-130");
            this.stateChanges.Should().Contain(SignedIn);
        }
    }

    [TestMethod]
    public async Task SignUp_ShouldReturnTheUserWithoutSigningIn_GivenConfirmationRequired()
    {
        this.StubSignUp(PendingConfirmationSignUp);
        var result = await this.client.SignUp(RandomEmail(), Password);
        using (new AssertionScope())
        {
            result!.User!.Id.Should().Be("user-id-259", "a sign-up awaiting confirmation still returns its user (issue #259)");
            result.AccessToken.Should().BeNull();
            this.client.CurrentSession.Should().BeNull();
            this.stateChanges.Should().NotContain(SignedIn);
        }
    }

    [TestMethod]
    public async Task SignUp_ShouldReturnNull_GivenAcknowledgementOnly()
    {
        this.StubSignUp(Acknowledgement);
        (await this.client.SignUp(RandomEmail(), Password)).Should()
            .BeNull("a body with no id is not a user, and must not be handed back as an empty one (issue #259)");
    }

    [TestMethod]
    public async Task SignUp_ShouldAdoptPendingPhoneUser_GivenUnconfirmedSessionsAllowed()
    {
        this.StubSignUp(PendingPhoneSignUp);
        this.client.Options.AllowUnconfirmedUserSessions = true;
        await this.client.SignUp(Constants.SignUpType.Phone, "+15555550123", Password);
        using (new AssertionScope())
        {
            this.client.CurrentUser!.Id.Should().Be("user-id-259-phone");
            this.client.CurrentSession!.AccessToken.Should().BeNull("no token is granted until the user confirms");
            this.stateChanges.Should().Contain(SignedIn);
        }
    }

    [TestMethod]
    public void SignUpWithPhone_ShouldThrowOnTheCall_GivenAnEmptyPhoneNumber()
    {
        // Not awaited: validation must throw on the call, not once the task is observed.
        Action signUp = () => _ = new Api(this.server.Url).SignUpWithPhone(string.Empty, Password);
        signUp.Should().Throw<GotrueException>();
    }
}
