#region

using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     Pins the exact bytes the passwordless (OTP / magic link) requests put on the wire, including the
///     <c>create_user</c> boolean, the empty <c>data</c> object and the phone <c>channel</c> enum mapping —
///     all serialization behaviours the System.Text.Json migration can change.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class PasswordlessApprovalTests : RequestApprovalFixture
{
    [TestMethod]
    public async Task OtpRequest_ShouldSerializeToExpectedPayload_GivenEmail()
    {
        await this.Api.SignInWithOtp(new SignInWithPasswordlessEmailOptions("user@example.com"));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task OtpRequest_ShouldSendRedirectToIntact_GivenRedirectWithOwnQuery()
    {
        await this.Api.SignInWithOtp(new SignInWithPasswordlessEmailOptions("user@example.com") { EmailRedirectTo = RedirectWithOwnQuery });
        this.ValidateQueryParameters("redirect_to", RedirectWithOwnQuery);
    }

    [TestMethod]
    public async Task OtpRequest_ShouldSerializeToExpectedPayload_GivenPhone()
    {
        await this.Api.SignInWithOtp(new SignInWithPasswordlessPhoneOptions("+15555550123"));
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }

    [TestMethod]
    public async Task MagicLinkRequest_ShouldSerializeToExpectedPayload_GivenEmail()
    {
        await this.Api.SendMagicLinkEmail("user@example.com");
        await this.Verify(this.EmittedRequestBody).UseDirectory("Data");
    }
}
