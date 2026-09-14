#region

using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.StatelessClient;

#endregion

namespace Gotrue.Tests.Stateless;

/// <summary>
///     Pending email and phone signups return the user without tokens (issue #259).
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class StatelessSignUpTests
{
    private const string PendingEmailSignUp =
        """
        { "id": "user-id-259", "aud": "authenticated", "email": "pending@example.com", "confirmation_sent_at": "2026-06-09T09:15:57Z" }
        """;

    private const string PendingPhoneSignUp =
        """
        { "id": "user-id-259-phone", "aud": "authenticated", "phone": "15555550123", "confirmation_sent_at": "2026-06-09T09:15:57Z" }
        """;

    [TestMethod]
    [DataRow(PendingEmailSignUp, Constants.SignUpType.Email, "pending@example.com", "user-id-259")]
    [DataRow(PendingPhoneSignUp, Constants.SignUpType.Phone, "+15555550123", "user-id-259-phone")]
    public async Task SignUp_ShouldReturnTheUserWithoutTokens_GivenConfirmationRequired(
        string body, Constants.SignUpType type, string identifier, string expectedId)
    {
        using var server = new MockGotrueServer();
        server.Given(Request.Create().WithPath("/signup").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));
        var options = new StatelessClientOptions { Url = server.Url };
        var result = await new StatelessClient().SignUp(type, identifier, Password, options);
        using (new AssertionScope())
        {
            result!.User!.Id.Should().Be(expectedId, "a pending user must not be discarded (issue #259)");
            result.AccessToken.Should().BeNull();
        }
    }
}
