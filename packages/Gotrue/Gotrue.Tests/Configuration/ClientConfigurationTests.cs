#region

using System.Threading.Tasks;
using FluentAssertions;
using Gotrue.Tests.Support;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using static Gotrue.Tests.TestUtils;
using static Supabase.Gotrue.Exceptions.FailureHint.Reason;

#endregion

namespace Gotrue.Tests.Configuration;

/// <summary>
///     End-to-end behaviour when the client is misconfigured: an unreachable project URL fails as
///     <see cref="FailureHint.Reason.Offline" />, and an admin call with an invalid service key fails as
///     <see cref="FailureHint.Reason.AdminTokenRequired" /> against the live stack.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class ClientConfigurationTests
{
    [TestMethod]
    public async Task SignUp_ShouldThrowOffline_GivenUnreachableUrl()
    {
        var client = new Client(new ClientOptions
        {
            Url = "https://badprojecturl.supabase.co",
            AllowUnconfirmedUserSessions = true,
        });
        var signUp = () => client.SignUpAsync(Constants.SignUpType.Email, RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should().Be(Offline);
    }

    [TestMethod]
    public async Task ListUsers_ShouldThrowAdminTokenRequired_GivenBadServiceKey()
    {
        var admin = TestClients.AdminAgainstCliStack("bad_service_key");
        var listUsers = () => admin.ListUsers();
        var exception = await listUsers.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should().Be(AdminTokenRequired);
    }
}
