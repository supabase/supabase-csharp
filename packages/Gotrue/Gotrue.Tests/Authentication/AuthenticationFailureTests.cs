#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Constants.AuthState;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     End-to-end sign-up / sign-in rejections against the live stack: each invalid credential surfaces the
///     matching <see cref="FailureHint.Reason" /> and leaves the current session alone.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class AuthenticationFailureTests : AuthClientFixture
{
    [TestMethod]
    public async Task SignUp_ShouldThrowUserBadPassword_GivenWeakPassword()
    {
        var signUp = () => this.Client.SignUp(RandomEmail(), "x");
        await this.VerifyRejected(signUp, UserBadPassword);
    }

    [TestMethod]
    public async Task SignUp_ShouldThrowUserBadEmailAddress_GivenInvalidEmail()
    {
        var signUp = () => this.Client.SignUp("not a real email address", Password);
        await this.VerifyRejected(signUp, UserBadEmailAddress);
    }

    [TestMethod]
    public async Task SignUp_ShouldThrowUserBadPhoneNumber_GivenEmptyPhone()
    {
        this.StateChanges.Should().BeEmpty();
        var signUp = () => this.Client.SignUp(Constants.SignUpType.Phone, "", Password,
            new SignUpOptions { Data = new Dictionary<string, object> { { "firstName", "Testing" } } });
        await this.VerifyRejected(signUp, UserBadPhoneNumber);
    }

    [TestMethod]
    public async Task SignUp_ShouldThrowUserAlreadyRegisteredAndKeepSession_GivenDuplicate()
    {
        var email = RandomEmail();
        await this.Client.SignUp(email, Password);
        this.StateChanges.Should().Contain(SignedIn);
        this.Persistence.LoadSession().Should().NotBeNull();
        this.StateChanges.Clear();
        var duplicate = () => this.Client.SignUp(email, Password);
        await this.VerifyRejected(duplicate, UserAlreadyRegistered);
        this.Persistence.LoadSession().Should().NotBeNull();
        this.Client.CurrentSession.Should().NotBeNull();
    }

    [TestMethod]
    public async Task SignIn_ShouldThrow_GivenWrongPassword()
    {
        var email = RandomEmail();
        await this.Client.SignUp(email, Password);
        await this.Client.SignOut();
        var signIn = () => this.Client.SignIn(email, Password + "$");
        await signIn.Should().ThrowAsync<GotrueException>();
    }

    [TestMethod]
    public async Task ResetPasswordForEmail_ShouldSucceed_GivenUnknownEmail()
    {
        (await this.Client.ResetPasswordForEmail(RandomEmail())).Should().BeTrue(
            "the server does not disclose whether an email is registered");
    }

    private async Task VerifyRejected(Func<Task> operation, FailureHint.Reason expected)
    {
        var exception = await operation.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should().Be(expected);
        this.StateChanges.Should().BeEmpty();
    }
}
