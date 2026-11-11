#region

using System.Collections.Generic;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;

#endregion

namespace Gotrue.Tests.SignUp;

/// <summary>
///     Pins the exact bytes SignUpAsync puts on the wire for each credential: an email sign-up, a phone sign-up with
///     its messaging channel, and the optional metadata and captcha token (OpenAPI <c>POST /signup</c>).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class SignUpApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task SignUpAsync_ShouldSendEmailAndPassword_GivenAnEmailSignUp()
    {
        await this.Api.SignUpAsync(SignUpType.Email, "user@example.com", "super-secret-password");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldSendTheSmsChannel_GivenAPhoneSignUp()
    {
        await this.Api.SignUpAsync(SignUpType.Phone, "+15555550123", "super-secret-password");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldSendTheWhatsAppChannel_GivenAPhoneSignUpOverWhatsApp()
    {
        await this.Api.SignUpAsync(SignUpType.Phone, "+15555550123", "super-secret-password",
            new SignUpOptions { Channel = SignInWithPasswordlessPhoneOptions.MessagingChannel.WHATSAPP });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task SignUpAsync_ShouldNestMetadataAndCaptcha_GivenUserDataAndACaptchaToken()
    {
        await this.Api.SignUpAsync(SignUpType.Email, "user@example.com", "super-secret-password",
            new SignUpOptions { Data = new Dictionary<string, object> { { "first_name", "Ada" }, { "age", 36 } }, CaptchaToken = "the-captcha" });
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
