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

namespace Gotrue.Tests.Network;

/// <summary>
///     End-to-end connectivity behaviour: pinging the live stack flips the client online, an unreachable host
///     flips it offline, and while offline the SDK short-circuits I/O as <see cref="FailureHint.Reason.Offline" />
///     until connectivity returns.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public class NetworkStatusTests
{
    private const string UnreachableUrl = "https://badprojecturl.supabase.co";

    [TestMethod]
    public async Task PingCheck_ShouldReportOnline_GivenReachableUrl()
    {
        var client = TestClients.AgainstCliStack();
        client.Online = false;
        var status = new NetworkStatus { Client = client };
        (await status.PingCheckAsync(TestClients.CliPingUrl)).Should().BeTrue();
        client.Online.Should().BeTrue();
        client.Online = false;
        await status.StartAsync(TestClients.CliPingUrl);
        client.Online.Should().BeTrue();
    }

    [TestMethod]
    public async Task PingCheck_ShouldReportOffline_GivenUnreachableUrl()
    {
        var client = new Client(new ClientOptions { AllowUnconfirmedUserSessions = true, Url = UnreachableUrl }) { Online = true };
        var status = new NetworkStatus { Client = client };
        (await status.PingCheckAsync(UnreachableUrl)).Should().BeFalse();
        client.Online.Should().BeFalse();
    }

    [TestMethod]
    public async Task SignUp_ShouldThrowOffline_GivenClientIsOffline()
    {
        var client = new Client(new ClientOptions { AllowUnconfirmedUserSessions = true, Url = UnreachableUrl });
        client.Online = false;
        var signUp = () => client.SignUp(RandomEmail(), Password);
        var exception = await signUp.Should().ThrowAsync<GotrueException>();
        exception.Which.Reason.Should().Be(Offline);
    }

    [TestMethod]
    public async Task RefreshToken_ShouldThrowOfflineThenRecoverWhenConnectivityReturns()
    {
        var client = TestClients.AgainstCliStack();
        client.Online = true;
        await client.SignUp(RandomEmail(), Password);
        client.Online = false;
        var refresh = () => client.RefreshToken();
        (await refresh.Should().ThrowAsync<GotrueException>()).Which.Reason.Should().Be(Offline);
        client.Online = true;
        await client.RefreshToken();
        client.Shutdown();
    }
}
