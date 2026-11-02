#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue.Mfa;
using static Supabase.Gotrue.Constants;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.Network;

/// <summary>
///     Pins that every client operation needing the auth server refuses to run while the client is offline, before
///     anything reaches the wire, against a stubbed server.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class OfflineClientContractTests
{
    private const string Email = "user@example.com";
    private const string Phone = "+15555550100";
    private const string Password = "a-password";
    private const string Token = "123456";
    private IGotrueClient<User, Session> client = null!;
    private MockGotrueServer server = null!;

    public static IEnumerable<object[]> OperationsNeedingTheServer =>
        new[]
        {
            Operation("SignUp", client => client.SignUp(SignUpType.Email, Email, Password)),
            Operation("SendMagicLink", client => client.SignIn(Email)),
            Operation("SignInWithIdToken", client => client.SignInWithIdToken(Provider.Google, "an-id-token")),
            Operation("SignInWithEmailOtp", client => client.SignInWithOtp(new SignInWithPasswordlessEmailOptions(Email))),
            Operation("SignInWithPhoneOtp", client => client.SignInWithOtp(new SignInWithPasswordlessPhoneOptions(Phone))),
            Operation("SignInWithPassword", client => client.SignIn(Email, Password)),
            Operation("SignInWithProvider", client => client.SignIn(Provider.Github)),
            Operation("SignInWithSSOProvider", client => client.SignInWithSSO(Guid.NewGuid())),
            Operation("SignInWithSSODomain", client => client.SignInWithSSO("example.com")),
            Operation("SignInAnonymously", client => client.SignInAnonymously()),
            Operation("VerifyMobileOtp", client => client.VerifyOTP(Phone, Token, MobileOtpType.SMS)),
            Operation("VerifyEmailOtp", client => client.VerifyOTP(Email, Token, EmailOtpType.Email)),
            Operation("VerifyTokenHash", client => client.VerifyTokenHash("a-token-hash")),
            Operation("LinkIdentity", client => client.LinkIdentity(Provider.Github, new SignInOptions())),
            Operation("LinkIdentityWithIdToken", client => client.LinkIdentityWithIdToken(new LinkIdentityWithIdTokenOptions(Provider.Google, "an-id-token"))),
            Operation("UnlinkIdentity", client => client.UnlinkIdentity(new UserIdentity())),
            Operation("GetUserIdentities", client => client.GetUserIdentitiesAsync()),
            Operation("ListPasskeys", client => client.ListPasskeysAsync()),
            Operation("DeletePasskey", client => client.DeletePasskeyAsync(Guid.NewGuid())),
            Operation("Update", client => client.Update(new UserAttributes { Email = Email })),
            Operation("Reauthenticate", client => client.Reauthenticate()),
            Operation("RefreshSession", client => client.RefreshSession()),
            Operation("RefreshTokenWithTokens", client => client.RefreshToken("an-access-token", "a-refresh-token")),
            Operation("RefreshToken", client => client.RefreshToken()),
            Operation("Enroll", client => client.Enroll(new MfaEnrollParams())),
            Operation("Challenge", client => client.Challenge(new MfaChallengeParams())),
            Operation("Verify", client => client.Verify(new MfaVerifyParams())),
            Operation("ChallengeAndVerify", client => client.ChallengeAndVerify(new MfaChallengeAndVerifyParams())),
            Operation("Unenroll", client => client.Unenroll(new MfaUnenrollParams())),
            Operation("GetRecoveryCodesStatus", client => client.GetRecoveryCodesStatusAsync()),
            Operation("GenerateRecoveryCodes", client => client.GenerateRecoveryCodesAsync()),
            Operation("VerifyRecoveryCode", client => client.VerifyRecoveryCodeAsync("a-recovery-code")),
            Operation("UnenrollRecoveryCodes", client => client.UnenrollRecoveryCodesAsync()),
        };

    [TestInitialize]
    public void TestInitializer()
    {
        this.server = new MockGotrueServer();
        this.client = TestClients.Against(this.server);
        this.SignIn();
        this.client.Online = false;
    }

    [TestCleanup]
    public void TestCleanup() => this.server.Dispose();

    [TestMethod]
    [DynamicData(nameof(OperationsNeedingTheServer))]
    public async Task Operation_ShouldThrowOffline_GivenClientIsOffline(string operation, Func<IGotrueClient<User, Session>, Task> call)
    {
        var calling = () => call(this.client);
        (await calling.Should().ThrowAsync<GotrueException>().WithMessage("Only supported when online")).Which.Reason.Should().Be(Offline,
            $"{operation} needs the auth server, which an offline client cannot reach");
    }

    [TestMethod]
    [DynamicData(nameof(OperationsNeedingTheServer))]
    public async Task Operation_ShouldNotSendARequest_GivenClientIsOffline(string operation, Func<IGotrueClient<User, Session>, Task> call)
    {
        var calling = () => call(this.client);
        await calling.Should().ThrowAsync<GotrueException>();
        this.server.CountReceivedRequests().Should().Be(0, $"{operation} is refused before the wire while offline");
    }

    private static object[] Operation(string name, Func<IGotrueClient<User, Session>, Task> call) => new object[] { name, call };

    private void SignIn()
    {
        var persistence = SessionPersistenceSubstitute.Tracking();
        persistence.SaveSession(new Session { AccessToken = "an-access-token", RefreshToken = "a-refresh-token", ExpiresIn = 3600, User = new User { Id = "user-id-123" } });
        this.client.SetPersistence(persistence);
        this.client.LoadSession();
    }
}
