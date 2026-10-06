using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Resend;

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Pins the exact bytes the resend requests put on the wire, including the <c>type</c> enum mappings
///     (<c>signup</c>, <c>sms</c>, <c>phone_change</c>, <c>email_change</c>) and whether the target is sent
///     as <c>email</c> or <c>phone</c>.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class ResendParametersContractTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task ResendRequest_ShouldSerializeToExpectedPayload_GivenSignUpType()
    {
        const string email = "user@supabase.com";
        var resend = new ResendParameters(
            type: ResendType.SignUp,
            email: email,
            phone: null,
            options: null
        );
        await this.Api.Resend(resend);

        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ResendRequest_ShouldSerializeToExpectedPayload_GivenEmailChangeType()
    {
        const string email = "user@supabase.com";
        var resend = new ResendParameters(
            type: ResendType.EmailChange,
            email: email,
            phone: null,
            options: null
        );
        await this.Api.Resend(resend);

        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ResendRequest_ShouldSerializeToExpectedPayload_GivenPhoneChangeType()
    {
        var resend = new ResendParameters(
            type: ResendType.PhoneChange,
            email: null,
            phone: "+5544998989898",
            options: null
        );
        await this.Api.Resend(resend);

        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ResendRequest_ShouldSerializeToExpectedPayload_GivenSmsType()
    {
        var resend = new ResendParameters(
            type: ResendType.Sms,
            email: null,
            phone: "+5544998989898",
            options: null
        );
        await this.Api.Resend(resend);

        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task ResendRequest_ShouldSerializeCaptchaToExpectedPayload_GivenOptions()
    {
        var resend = new ResendParameters(
            type: ResendType.SignUp,
            email: "user@supabase.com",
            phone: null,
            options: new ResendOptions(captchaToken: "captcha-token", emailRedirectTo: null)
        );

        await this.Api.Resend(resend);

        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
