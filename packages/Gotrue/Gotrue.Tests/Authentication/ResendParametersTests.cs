using System.Net;
using System.Threading.Tasks;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue.Resend;
using static Gotrue.Tests.TestUtils;

namespace Gotrue.Tests.Authentication;

[TestClass]
[TestCategory("E2E")]
public class ResendParametersTests : AuthClientFixture
{
    [TestMethod]
    public async Task Resend_ShouldRequestConfirmationCode_GivenSignUpType()
    {
        var email = RandomEmail();
        var resend = new ResendParameters(ResendType.SignUp) { Email = email };
        var response = await this.Client.Resend(resend);

        Assert.IsNotNull(response);
        Assert.AreEqual(HttpStatusCode.OK, response?.ResponseMessage?.StatusCode);
    }

    [TestMethod]
    public async Task Resend_ShouldRequestConfirmationCode_GivenSmsType()
    {
        var resend = new ResendParameters(ResendType.Sms) { Phone = "5544989899898" };
        var response = await this.Client.Resend(resend);

        Assert.IsNotNull(response);
        Assert.AreEqual(HttpStatusCode.OK, response?.ResponseMessage?.StatusCode);
    }

    [TestMethod]
    public async Task Resend_ShouldRequestConfirmationCode_GivenPhoneChangeType()
    {
        var resend = new ResendParameters(ResendType.PhoneChange) { Phone = "5544989899898" };
        var response = await this.Client.Resend(resend);

        Assert.IsNotNull(response);
        Assert.AreEqual(HttpStatusCode.OK, response?.ResponseMessage?.StatusCode);
    }

    [TestMethod]
    public async Task Resend_ShouldRequestConfirmationCode_GivenEmailChangeType()
    {
        var resend = new ResendParameters(ResendType.EmailChange) { Email = RandomEmail() };
        var response = await this.Client.Resend(resend);

        Assert.IsNotNull(response);
        Assert.AreEqual(HttpStatusCode.OK, response?.ResponseMessage?.StatusCode);
    }
}
