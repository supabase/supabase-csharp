#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

#endregion

namespace Gotrue.Tests.Authentication;

/// <summary>
///     A secure email change is acknowledged with <c>{ msg, code }</c>, which VerifyOTP must report as null
///     rather than read as a user (issue #259).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class VerifyOtpTests
{
    private const string EmailChangeAccepted =
        """
        { "msg": "Confirmation link accepted. Please proceed to confirm link sent to the other email", "code": 200 }
        """;

    [TestMethod]
    public async Task VerifyOTP_ShouldReturnNull_GivenEmailChangeAcknowledgement()
    {
        using var server = new MockGotrueServer();
        server.Given(Request.Create().WithPath("/verify").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(EmailChangeAccepted));
        var client = TestClients.Against(server);
        (await client.VerifyOTP("user@example.com", "123456", Constants.EmailOtpType.EmailChange)).Should()
            .BeNull("the acknowledgement carries no session (issue #259)");
        client.CurrentSession.Should().BeNull();
    }
}
